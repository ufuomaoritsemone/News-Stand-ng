using System.Buffers.Binary;
using System.Globalization;
using System.Net.WebSockets;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace TtsWorker.Synthesizers;

/// <summary>
/// Architecture Component: Tri-Tier TTS Architecture — Tier 2 (Option B): Microsoft Edge Neural TTS Engine.
/// 
/// Purpose:
/// Provides production-quality, authentic Nigerian English neural voice synthesis without requiring any API keys,
/// credit cards, or external cloud subscriptions ($0 operational cost).
/// </summary>
public sealed class EdgeNeuralTtsSynthesizer : ITtsSynthesizer
{
    private readonly ILogger<EdgeNeuralTtsSynthesizer> _logger;

    private const string TrustedClientToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    private const string SecMsGecVersion = "1-130.0.2849.68";
    private const string EdgeWssBaseUrl = "wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1";
    private const string DefaultVoice = "en-NG-EzinneNeural";

    public string ProviderName => "EdgeNeural";

    // Configured out-of-the-box with zero required API keys
    public bool IsConfigured => true;

    public EdgeNeuralTtsSynthesizer(ILogger<EdgeNeuralTtsSynthesizer> logger)
    {
        _logger = logger;
    }

    private static string GenerateSecMsGec()
    {
        // Windows file time is measured in 100-nanosecond intervals since 1601-01-01 UTC.
        // Round down to the nearest 5-minute window (300 seconds * 10,000,000 ticks/sec = 3,000,000,000 ticks).
        var fileTime = DateTime.UtcNow.ToFileTimeUtc();
        var roundedTicks = fileTime - (fileTime % 3_000_000_000L);
        var strToHash = $"{roundedTicks}{TrustedClientToken}";
        var hashBytes = SHA256.HashData(Encoding.ASCII.GetBytes(strToHash));
        return Convert.ToHexString(hashBytes);
    }

    public async Task<TtsSynthesisResult> SynthesizeAsync(
        string text,
        string? voiceOrLanguage = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TtsSynthesisResult(false, null, "audio/mpeg", ProviderName, "Empty text provided for synthesis.");
        }

        var voice = !string.IsNullOrWhiteSpace(voiceOrLanguage) ? voiceOrLanguage : DefaultVoice;
        var connectionId = Guid.NewGuid().ToString("N");
        var secMsGec = GenerateSecMsGec();
        var wssUri = new Uri($"{EdgeWssBaseUrl}?TrustedClientToken={TrustedClientToken}&Sec-MS-GEC={secMsGec}&Sec-MS-GEC-Version={SecMsGecVersion}&ConnectionId={connectionId}");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(25)); // 25-second synthesis deadline

        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 Edg/130.0.0.0");
        ws.Options.SetRequestHeader("Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");
        ws.Options.SetRequestHeader("Pragma", "no-cache");
        ws.Options.SetRequestHeader("Cache-Control", "no-cache");

        try
        {
            _logger.LogInformation("Connecting to Edge Neural TTS gateway for voice '{Voice}'...", voice);
            await ws.ConnectAsync(wssUri, cts.Token);

            var timestamp = DateTime.UtcNow.ToString("ddd MMM dd yyyy HH:mm:ss 'GMT+0000 (Coordinated Universal Time)'", CultureInfo.InvariantCulture);

            // 1. Send speech configuration message
            var configPayload = "{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":{\"sentenceBoundaryEnabled\":\"false\",\"wordBoundaryEnabled\":\"false\"},\"outputFormat\":\"audio-24khz-48kbitrate-mono-mp3\"}}}}";
            var configMessage = $"X-Timestamp:{timestamp}\r\nContent-Type:application/json; charset=utf-8\r\nPath:speech.config\r\n\r\n{configPayload}";
            await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(configMessage)), WebSocketMessageType.Text, true, cts.Token);

            // 2. Build and transmit SSML
            var requestId = Guid.NewGuid().ToString("N");
            var cleanText = SecurityElement.Escape(text.Trim());
            var ssml = $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'><voice name='{voice}'><prosody pitch='+0Hz' rate='+0%' volume='+0%'>{cleanText}</prosody></voice></speak>";
            var ssmlMessage = $"X-RequestId:{requestId}\r\nX-Timestamp:{timestamp}\r\nContent-Type:application/ssml+xml\r\nPath:ssml\r\n\r\n{ssml}";
            await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(ssmlMessage)), WebSocketMessageType.Text, true, cts.Token);

            // 3. Receive binary audio stream fragments
            using var audioStream = new MemoryStream();
            var receiveBuffer = new byte[16 * 1024];

            while (ws.State == WebSocketState.Open && !cts.Token.IsCancellationRequested)
            {
                using var messageBuffer = new MemoryStream();
                WebSocketReceiveResult receiveResult;

                do
                {
                    receiveResult = await ws.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), cts.Token);
                    messageBuffer.Write(receiveBuffer, 0, receiveResult.Count);
                }
                while (!receiveResult.EndOfMessage);

                if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                var messageBytes = messageBuffer.ToArray();

                if (receiveResult.MessageType == WebSocketMessageType.Text)
                {
                    var textMessage = Encoding.UTF8.GetString(messageBytes);
                    if (textMessage.Contains("Path:turn.end", StringComparison.OrdinalIgnoreCase))
                    {
                        // Turn complete — all audio packets received
                        break;
                    }
                }
                else if (receiveResult.MessageType == WebSocketMessageType.Binary && messageBytes.Length > 2)
                {
                    // Binary packet structure:
                    // 2 bytes: Big-Endian unsigned 16-bit integer (header length L)
                    // L bytes: UTF-8 text header (contains "Path:audio\r\n...")
                    // Remaining bytes: raw MP3 audio payload
                    ushort headerLength = BinaryPrimitives.ReadUInt16BigEndian(messageBytes.AsSpan(0, 2));
                    int audioOffset = 2 + headerLength;

                    if (messageBytes.Length > audioOffset)
                    {
                        var headerSpan = messageBytes.AsSpan(2, Math.Min((int)headerLength, messageBytes.Length - 2));
                        var headerStr = Encoding.UTF8.GetString(headerSpan);

                        if (headerStr.Contains("Path:audio", StringComparison.OrdinalIgnoreCase))
                        {
                            audioStream.Write(messageBytes, audioOffset, messageBytes.Length - audioOffset);
                        }
                    }
                }
            }

            try
            {
                if (ws.State == WebSocketState.Open)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Completed", CancellationToken.None);
                }
            }
            catch
            {
                // Socket cleanup suppression
            }

            var audioData = audioStream.ToArray();
            if (audioData.Length == 0)
            {
                _logger.LogWarning("Edge Neural TTS gateway connected but returned zero audio bytes.");
                return new TtsSynthesisResult(false, null, "audio/mpeg", ProviderName, "No audio payload received from speech gateway.");
            }

            _logger.LogInformation("Successfully synthesized {Bytes} bytes of authentic Nigerian neural audio via Edge Neural ({Voice}).", audioData.Length, voice);
            return new TtsSynthesisResult(true, audioData, "audio/mpeg", ProviderName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Edge Neural TTS synthesis encountered an error ({Error}). Seamless fallback will be engaged.", ex.Message);
            return new TtsSynthesisResult(false, null, "audio/mpeg", ProviderName, ex.Message);
        }
    }
}
