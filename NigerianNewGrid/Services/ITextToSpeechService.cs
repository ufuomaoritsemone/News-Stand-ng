using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid.Services;

public interface ITextToSpeechService
{
    bool IsSpeaking { get; }
    bool IsPaused { get; }
    Task<bool> SpeakAsync(string text, string? language = null, CancellationToken cancellationToken = default);
    Task<bool> SpeakBriefingAsync(IEnumerable<BriefingItem> items, string? language = null, CancellationToken cancellationToken = default);
    void Pause();
    Task<bool> ResumeBriefingAsync(string? language = null, CancellationToken cancellationToken = default);
    void Cancel();
}
