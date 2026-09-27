using System;
using NigerianNewsGrid.Client.Helpers;
using NigerianNewGrid.Services;
using Xunit;

namespace NewsApiClient.Tests;

public class NotificationPreferencesTests
{
    [Fact]
    public void GetNextAudioBriefingUtc_BeforeMorningSchedule_TargetsMorningSlot()
    {
        // 05:00 UTC = 06:00 WAT on Sept 1, 2026 (before 08:00 WAT)
        var utc = new DateTime(2026, 9, 1, 5, 0, 0, DateTimeKind.Utc);
        var nextUtc = TtsBriefingFormatter.GetNextAudioBriefingUtc(utc);

        // Expected: 08:00 WAT = 07:00 UTC on Sept 1, 2026
        Assert.Equal(new DateTime(2026, 9, 1, 7, 0, 0, DateTimeKind.Utc), nextUtc);
    }

    [Fact]
    public void GetNextAudioBriefingUtc_BetweenMorningAndEvening_TargetsEveningSlot()
    {
        // 09:00 UTC = 10:00 WAT on Sept 1, 2026 (after 08:00 WAT, before 18:00 WAT)
        var utc = new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc);
        var nextUtc = TtsBriefingFormatter.GetNextAudioBriefingUtc(utc);

        // Expected: 18:00 WAT = 17:00 UTC on Sept 1, 2026
        Assert.Equal(new DateTime(2026, 9, 1, 17, 0, 0, DateTimeKind.Utc), nextUtc);
    }

    [Fact]
    public void GetNextAudioBriefingUtc_AfterEveningSchedule_TargetsTomorrowMorningSlot()
    {
        // 19:00 UTC = 20:00 WAT on Sept 1, 2026 (after 18:00 WAT)
        var utc = new DateTime(2026, 9, 1, 19, 0, 0, DateTimeKind.Utc);
        var nextUtc = TtsBriefingFormatter.GetNextAudioBriefingUtc(utc);

        // Expected: 08:00 WAT = 07:00 UTC on Sept 2, 2026
        Assert.Equal(new DateTime(2026, 9, 2, 7, 0, 0, DateTimeKind.Utc), nextUtc);
    }

    [Theory]
    [InlineData(7, "2026-09-01_morning")]   // 07:00 UTC = 08:00 WAT
    [InlineData(11, "2026-09-01_Evening")]  // 11:00 UTC = 12:00 WAT
    [InlineData(17, "2026-09-01_Evening")]  // 17:00 UTC = 18:00 WAT
    public void GetCurrentAudioBriefingCycle_GeneratesExpectedCycleKeys(int utcHour, string expectedKey)
    {
        var utc = new DateTime(2026, 9, 1, utcHour, 0, 0, DateTimeKind.Utc);
        var cycle = TtsBriefingFormatter.GetCurrentAudioBriefingCycle(utc);
        Assert.Equal(expectedKey, cycle);
    }

    [Fact]
    public void AppNotificationBridge_RaisesEventWhenTriggered()
    {
        bool triggered = false;
        Action handler = () => triggered = true;

        AppNotificationBridge.PlayAudioBriefingRequested += handler;
        try
        {
            AppNotificationBridge.TriggerAutoPlayAudioBriefing();
            Assert.True(triggered);
        }
        finally
        {
            AppNotificationBridge.PlayAudioBriefingRequested -= handler;
        }
    }
}
