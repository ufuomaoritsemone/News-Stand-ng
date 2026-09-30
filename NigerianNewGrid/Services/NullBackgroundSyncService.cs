using System.Diagnostics;

namespace NigerianNewGrid.Services;

/// <summary>
/// Fallback no-op background synchronization service for unsupported platforms or test environments.
/// </summary>
public sealed class NullBackgroundSyncService : IBackgroundSyncService
{
    public void ScheduleNewsSync(bool immediate = false)
    {
        Debug.WriteLine($"[NullBackgroundSyncService] ScheduleNewsSync called (immediate: {immediate}).");
    }

    public void CancelNewsSync()
    {
        Debug.WriteLine("[NullBackgroundSyncService] CancelNewsSync called.");
    }

    public void TriggerWidgetRefresh()
    {
        Debug.WriteLine("[NullBackgroundSyncService] TriggerWidgetRefresh called.");
    }
}
