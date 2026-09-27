using System;
using NigerianNewsGrid.Client.Helpers;
using Xunit;

namespace NewsApiClient.Tests;

public class TtsBriefingFormatterTests
{
    [Theory]
    [InlineData(1, 9, 2026, "1st september, 2026")]
    [InlineData(2, 9, 2026, "2nd september, 2026")]
    [InlineData(3, 9, 2026, "3rd september, 2026")]
    [InlineData(4, 9, 2026, "4th september, 2026")]
    [InlineData(11, 9, 2026, "11th september, 2026")]
    [InlineData(12, 9, 2026, "12th september, 2026")]
    [InlineData(13, 9, 2026, "13th september, 2026")]
    [InlineData(21, 9, 2026, "21st september, 2026")]
    [InlineData(22, 9, 2026, "22nd september, 2026")]
    [InlineData(23, 9, 2026, "23rd september, 2026")]
    [InlineData(31, 8, 2026, "31st august, 2026")]
    public void FormatBriefingDate_FormatsOrdinalDateCorrectly(int day, int month, int year, string expected)
    {
        var dt = new DateTime(year, month, day);
        var result = TtsBriefingFormatter.FormatBriefingDate(dt);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(8, "morning")]
    [InlineData(10, "morning")]
    [InlineData(11, "morning")]
    [InlineData(12, "Evening")]
    [InlineData(18, "Evening")]
    [InlineData(20, "Evening")]
    public void GetTimeOfDay_ReturnsMorningOrEveningBasedOnSchedule(int hour, string expectedTimeOfDay)
    {
        var dt = new DateTime(2026, 9, 1, hour, 0, 0);
        var result = TtsBriefingFormatter.GetTimeOfDay(dt);
        Assert.Equal(expectedTimeOfDay, result);
    }

    [Fact]
    public void BuildBriefingIntro_ReturnsCorrectPattern()
    {
        // 07:00 UTC = 08:00 WAT (Nigerian Time) on Sept 1, 2026
        var utcMorning = new DateTime(2026, 9, 1, 7, 0, 0, DateTimeKind.Utc);
        var morningIntro = TtsBriefingFormatter.BuildBriefingIntro(utcMorning);
        Assert.Equal("this is the morning headline briefing for today 1st september, 2026", morningIntro);

        // 17:00 UTC = 18:00 WAT (Nigerian Time) on Sept 1, 2026
        var utcEvening = new DateTime(2026, 9, 1, 17, 0, 0, DateTimeKind.Utc);
        var eveningIntro = TtsBriefingFormatter.BuildBriefingIntro(utcEvening);
        Assert.Equal("this is the Evening headline briefing for today 1st september, 2026", eveningIntro);
    }

    [Theory]
    [InlineData(
        "Repeated xenophobic violence in South Africa threatens the dream of Pan-African unity.\n\nRead More: https://punchng.com/the-dislocated-dream/",
        "Repeated xenophobic violence in South Africa threatens the dream of Pan-African unity.")]
    [InlineData(
        "Ahead of Saturday's Osun election, an APC chieftain warns party members against violence. To read more, visit Punch Online.",
        "Ahead of Saturday's Osun election, an APC chieftain warns party members against violence.")]
    [InlineData(
        "Youri Tielemans issues an apology to Aston Villa fans. Read more on hi\n\nRead More: https://punchng.com/epl/",
        "Youri Tielemans issues an apology to Aston Villa fans.")]
    [InlineData(
        "President Tinubu appoints Yakubu Jang as Senior Special Assistant, announced at Jang&#8217;s 80th birthday. [&#8230;]\n\nRead More: https://punchng.com/tinubu/",
        "President Tinubu appoints Yakubu Jang as Senior Special Assistant, announced at Jang's 80th birthday.")]
    [InlineData(
        "<p>SMEDAN launches the BRYNE project to support young entrepreneurs.</p> Continue reading https://guardian.ng/smedan",
        "SMEDAN launches the BRYNE project to support young entrepreneurs.")]
    [InlineData(
        "Click here to read more on the latest market trends: https://nairametrics.com",
        "")]
    public void CleanTextForTts_RemovesReadMoreAndSanitizesText(string raw, string expected)
    {
        var result = TtsBriefingFormatter.CleanTextForTts(raw);
        Assert.Equal(expected, result);
    }
}
