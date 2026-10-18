using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid.Services;

public interface IAudioBriefingCoordinator : IDisposable
{
    bool IsSpeaking { get; }
    bool IsPaused { get; }
    string AudioButtonText { get; }
    string AudioButtonIcon { get; }
    string AudioButtonHint { get; }
    event EventHandler? StateChanged;

    Task TogglePlayPauseAsync(IReadOnlyList<BriefingCategory> categories, string preferredLanguage);
    void Cancel();
}
