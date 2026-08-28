using System.Diagnostics;
using System.Text;
using Microsoft.Maui.Media;
using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid.Services;

public class MauiTextToSpeechService : ITextToSpeechService
{
    private CancellationTokenSource? _ttsCts;
    private bool _isSpeaking;

    public bool IsSpeaking => _isSpeaking;

    public async Task SpeakAsync(string text, string? language = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        Cancel();
        _ttsCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _isSpeaking = true;

        try
        {
            var speechOptions = new SpeechOptions
            {
                Volume = 1.0f,
                Pitch = 1.0f
            };

            // Attempt to match matching platform voice locale if available
            var locales = await TextToSpeech.Default.GetLocalesAsync();
            var matchedLocale = locales.FirstOrDefault(l =>
                !string.IsNullOrWhiteSpace(language) &&
                (l.Language.Contains(language, StringComparison.OrdinalIgnoreCase) ||
                 l.Name.Contains(language, StringComparison.OrdinalIgnoreCase)));

            if (matchedLocale != null)
            {
                speechOptions.Locale = matchedLocale;
            }

            await TextToSpeech.Default.SpeakAsync(text, speechOptions, _ttsCts.Token);
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("[TextToSpeech] Speech playback was cancelled.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TextToSpeech] Synthesis error: {ex.Message}");
        }
        finally
        {
            _isSpeaking = false;
        }
    }

    public async Task SpeakBriefingAsync(IEnumerable<BriefingItem> items, string? language = null, CancellationToken cancellationToken = default)
    {
        var list = items.ToList();
        if (list.Count == 0) return;

        var sb = new StringBuilder();
        sb.AppendLine($"Here is your {language ?? "daily"} news briefing.");

        int count = 1;
        foreach (var item in list.Take(10))
        {
            sb.AppendLine($"Headline {count}: {item.Title}.");
            if (!string.IsNullOrWhiteSpace(item.Summary))
            {
                sb.AppendLine(item.Summary);
            }
            sb.AppendLine();
            count++;
        }

        sb.AppendLine("That concludes your news summary.");
        await SpeakAsync(sb.ToString(), language, cancellationToken);
    }

    public void Cancel()
    {
        try
        {
            _ttsCts?.Cancel();
            _ttsCts?.Dispose();
            _ttsCts = null;
        }
        catch { }
        finally
        {
            _isSpeaking = false;
        }
    }
}
