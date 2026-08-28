using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid.Services;

public interface ITextToSpeechService
{
    bool IsSpeaking { get; }
    Task SpeakAsync(string text, string? language = null, CancellationToken cancellationToken = default);
    Task SpeakBriefingAsync(IEnumerable<BriefingItem> items, string? language = null, CancellationToken cancellationToken = default);
    void Cancel();
}
