namespace NigerianNewGrid.Services;

/// <summary>
/// Facilitates cross-platform communication between native platform notification handlers
/// (Android MainActivity, iOS NotificationDelegate) and the MAUI UI/ViewModels.
/// </summary>
public static class AppNotificationBridge
{
    /// <summary>
    /// Raised when an audio briefing notification is tapped and audio playback should begin.
    /// </summary>
    public static event Action? PlayAudioBriefingRequested;

    /// <summary>
    /// Raised when background sync or push updates persist fresh news stories to SQLite.
    /// Passes the count of new stories inserted.
    /// </summary>
    public static event Action<int>? NewStoriesAvailable;

    /// <summary>
    /// Raised when the application resumes from the background / sleep state.
    /// </summary>
    public static event Action? AppResumed;

    /// <summary>
    /// Signals that audio briefing playback should begin immediately.
    /// </summary>
    public static void TriggerAutoPlayAudioBriefing()
    {
        PlayAudioBriefingRequested?.Invoke();
    }

    /// <summary>
    /// Signals that the application has returned to the foreground.
    /// </summary>
    public static void TriggerAppResumed()
    {
        AppResumed?.Invoke();
    }

    /// <summary>
    /// Signals that fresh news stories have been persisted to local storage in the background.
    /// </summary>
    public static void NotifyNewStoriesAvailable(int count)
    {
        if (count > 0)
        {
            NewStoriesAvailable?.Invoke(count);
        }
    }
}
