namespace NigerianNewGrid.Services;

/// <summary>
/// Static delegation facade for <see cref="IAppEventBus"/>.
/// Preserves static calling convention for native broadcast receivers and platform delegates
/// while delegating to the thread-safe, weak-event bus to prevent subscriber memory leaks (Issue #18).
/// </summary>
public static class AppNotificationBridge
{
    private static IAppEventBus Bus =>
#if ANDROID || IOS || MACCATALYST || WINDOWS
        IPlatformApplication.Current?.Services.GetService<IAppEventBus>() ?? AppEventBus.Instance;
#else
        AppEventBus.Instance;
#endif

    /// <summary>
    /// Raised when an audio briefing notification is tapped and audio playback should begin.
    /// </summary>
    public static event Action? PlayAudioBriefingRequested
    {
        add => Bus.PlayAudioBriefingRequested += value;
        remove => Bus.PlayAudioBriefingRequested -= value;
    }

    /// <summary>
    /// Raised when background sync or push updates persist fresh news stories to SQLite.
    /// Passes the count of new stories inserted.
    /// </summary>
    public static event Action<int>? NewStoriesAvailable
    {
        add => Bus.NewStoriesAvailable += value;
        remove => Bus.NewStoriesAvailable -= value;
    }

    /// <summary>
    /// Raised when the application resumes from the background / sleep state.
    /// </summary>
    public static event Action? AppResumed
    {
        add => Bus.AppResumed += value;
        remove => Bus.AppResumed -= value;
    }

    /// <summary>
    /// Signals that audio briefing playback should begin immediately.
    /// </summary>
    public static void TriggerAutoPlayAudioBriefing() => Bus.TriggerAutoPlayAudioBriefing();

    /// <summary>
    /// Signals that the application has returned to the foreground.
    /// </summary>
    public static void TriggerAppResumed() => Bus.TriggerAppResumed();

    /// <summary>
    /// Signals that fresh news stories have been persisted to local storage in the background.
    /// </summary>
    public static void NotifyNewStoriesAvailable(int count) => Bus.NotifyNewStoriesAvailable(count);
}
