namespace NigerianNewGrid.Services;

/// <summary>
/// Event bus interface for application-wide decoupling and notifications (Issue #18).
/// Provides weak-referenced event dispatch to prevent memory leaks in transient ViewModels.
/// </summary>
public interface IAppEventBus
{
    void TriggerAutoPlayAudioBriefing();
    void TriggerAppResumed();
    void NotifyNewStoriesAvailable(int count);

    event Action? PlayAudioBriefingRequested;
    event Action<int>? NewStoriesAvailable;
    event Action? AppResumed;
}
