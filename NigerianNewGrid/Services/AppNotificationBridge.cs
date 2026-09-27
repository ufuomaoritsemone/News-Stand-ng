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
    /// Signals that audio briefing playback should begin immediately.
    /// </summary>
    public static void TriggerAutoPlayAudioBriefing()
    {
        PlayAudioBriefingRequested?.Invoke();
    }
}
