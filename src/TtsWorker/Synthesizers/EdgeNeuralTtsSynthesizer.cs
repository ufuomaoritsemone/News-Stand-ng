using System.Buffers.Binary;
using System.Net.WebSockets;
using System.Security;
using System.Text;
using Microsoft.Extensions.Logging;

namespace TtsWorker.Synthesizers;

/// <summary>
/// Architecture Component: Tri-Tier TTS Architecture — Tier 2 (Option B): Microsoft Edge Neural TTS Engine.
/// 
/// Purpose:
/// Provides production-quality, authentic Nigerian English neural voice synthesis without requiring any API keys,
/// credit cards, or external cloud subscriptions ($0 operational cost).
/// 
/// Technical Mechanism:
/// - Connects to Microsoft Speech synthesis gateway over a secure TLS WebSocket connection (WSS).
/// - Transmits W3C SSML (Speech Synthesis Markup Language) specifying Nigerian neural voices:
///     • "en-NG-EzinneNeural" (Default female voice with natural Nigerian English intonation & inflection)
///     • "en-NG-AbeoNeural" (Male Nigerian English voice)
/// - Receives real-time streaming MP3 frames formatted as "audio-24khz-48kbitrate-mono-mp3".
/// - Parses the binary transport frames (stripping 2-byte length headers and metadata headers) and assembles
///   the resulting high-fidelity MP3 audio stream.
/// - Built-in resilience: In the event of network disruption, rate limits, or endpoint changes, synthesis
///   gracefully returns a failure result so CompositeTtsSynthesizer seamlessly falls back to Tier 3 (LocalFallback).
/// </summary>
public sealed class EdgeNeuralTtsSynthesizer : ITtsSynthesizer
{
    private readonly ILogger<EdgeNeuralTtsSynthesizer> _logger;

    private const string EdgeWssUrl = "wss://speech.platform.bing.com/consumer/speech/synthesize/readaheadedge/v1?TrustedClientToken=6A5AA1D4EAFF4E9FB37E23D68491D6F4&ConnectionId=";
    private const string DefaultVoice = "en-NG-EzinneNeural";

    public string ProviderName => "EdgeNeural";

    // Configured out-of-the-box with zero required API keys
    public bool IsConfigured => true;

    public EdgeNeuralTtsSynthesizer(ILogger<EdgeNeuralTtsSynthesizer> logger)
    {
        _logger = logger;
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
        var wssUri = new Uri($"{EdgeWssUrl}{connectionId}");

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

            // 1. Send speech configuration message
            var configPayload = "{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":{\"sentenceBoundaryEnabled\":\"false\",\"wordBoundaryEnabled\":\"false\"},\"outputFormat\":\"audio-24khz-48kbitrate-mono-mp3\"}}}}";
            var configMessage = $"Content-Type:application/json; charset=utf-8\r\nPath:speech.config\r\n\r\n{configPayload}";
            await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(configMessage)), WebSocketMessageType.Text, true, cts.Token);

            // 2. Build and transmit SSML
            var requestId = Guid.NewGuid().ToString("N");
            var cleanText = SecurityElement.Escape(text.Trim());
            var ssml = $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'><voice name='{voice}'><prosody pitch='+0Hz' rate='+0%' volume='+0%'>{cleanText}</prosody></voice></speak>";
            var ssmlMessage = $"X-RequestId:{requestId}\r\nContent-Type:application/ssml+xml\r\nPath:ssml\r\n\r\n{ssml}";
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
