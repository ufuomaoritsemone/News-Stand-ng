using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace NigerianNewsGrid.Client.Helpers;

/// <summary>
/// Formats text-to-speech briefing intros, timestamps according to Nigerian Standard Time (WAT, UTC+1),
/// and strips reading teasers ('to read more', URLs, HTML entities) from news summaries.
/// </summary>
public static class TtsBriefingFormatter
{
    /// <summary>
    /// Gets current time in Nigerian Standard Time (West Africa Time, WAT = UTC+1, no DST).
    /// </summary>
    public static DateTime GetNigerianTime(DateTime? utcNow = null)
    {
        var utc = utcNow ?? DateTime.UtcNow;
        try
        {
            var tz = TimeZoneInfo.GetSystemTimeZones()
                .FirstOrDefault(t => t.Id == "W. Central Africa Standard Time" ||
                                     t.Id == "Africa/Lagos" ||
                                     t.DisplayName.Contains("West Africa Time", StringComparison.OrdinalIgnoreCase));
            return tz != null ? TimeZoneInfo.ConvertTimeFromUtc(utc, tz) : utc.AddHours(1);
        }
        catch
        {
            return utc.AddHours(1);
        }
    }

    /// <summary>
    /// Returns "morning" or "Evening" based on the scheduled update windows (8am and 6pm).
    /// Before 12:00 PM is morning; 12:00 PM onwards is Evening.
    /// </summary>
    public static string GetTimeOfDay(DateTime nigerianTime)
    {
        return nigerianTime.Hour < 12 ? "morning" : "Evening";
    }

    /// <summary>
    /// Gets the audio briefing cycle identifier, e.g. "2026-09-01_morning" or "2026-09-01_Evening".
    /// </summary>
    public static string GetCurrentAudioBriefingCycle(DateTime? utcNow = null)
    {
        var wat = GetNigerianTime(utcNow);
        var timeOfDay = GetTimeOfDay(wat);
        return $"{wat:yyyy-MM-dd}_{timeOfDay}";
    }

    /// <summary>
    /// Calculates the next scheduled audio briefing time (8:00 AM or 6:00 PM WAT) in UTC.
    /// </summary>
    public static DateTime GetNextAudioBriefingUtc(DateTime? utcNow = null)
    {
        var nowUtc = utcNow ?? DateTime.UtcNow;
        var wat = GetNigerianTime(nowUtc);

        var today8amWat = wat.Date.AddHours(8);
        var today6pmWat = wat.Date.AddHours(18);
        var tomorrow8amWat = wat.Date.AddDays(1).AddHours(8);

        DateTime nextWat;
        if (wat < today8amWat)
            nextWat = today8amWat;
        else if (wat < today6pmWat)
            nextWat = today6pmWat;
        else
            nextWat = tomorrow8amWat;

        return DateTime.SpecifyKind(nextWat.AddHours(-1), DateTimeKind.Utc);
    }

    /// <summary>
    /// Formats the date with ordinal day suffix and lowercase month name, e.g. "1st september, 2026".
    /// </summary>
    public static string FormatBriefingDate(DateTime date)
    {
        int day = date.Day;
        string suffix = (day % 100) switch
        {
            11 or 12 or 13 => "th",
            _ => (day % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            }
        };

        string month = date.ToString("MMMM", System.Globalization.CultureInfo.InvariantCulture).ToLowerInvariant();
        return $"{day}{suffix} {month}, {date.Year}";
    }

    /// <summary>
    /// Builds the standard opening phrase:
    /// "this is the <<time-of-day>> headline briefing for today <<date>>"
    /// e.g. "this is the morning headline briefing for today 1st september, 2026"
    /// </summary>
    public static string BuildBriefingIntro(DateTime? utcNow = null)
    {
        var nigerianTime = GetNigerianTime(utcNow);
        var timeOfDay = GetTimeOfDay(nigerianTime);
        var formattedDate = FormatBriefingDate(nigerianTime);
        return $"this is the {timeOfDay} headline briefing for today {formattedDate}";
    }

    /// <summary>
    /// Cleans headline and summary text for natural text-to-speech reading by:
    /// - Stripping out URLs (http:// and https://)
    /// - Removing 'to read more', 'read more', 'continue reading' and any trailing links/text
    /// - Stripping HTML tags, brackets ([...]), and decoding HTML entities (&amp;, &#8217;, etc.)
    /// - Trimming trailing colons, hyphens, and whitespace
    /// </summary>
    public static string CleanTextForTts(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // 1. Decode HTML entities (&amp;, &#8217;, etc.)
        string clean = WebUtility.HtmlDecode(text)
            .Replace('\u2019', '\'')
            .Replace('\u2018', '\'');

        // 2. Remove HTML tags if present (<p>, <br>, etc.)
        clean = Regex.Replace(clean, @"<[^>]+>", " ");

        // 3. Remove web URLs
        clean = Regex.Replace(clean, @"https?://\S+", string.Empty, RegexOptions.IgnoreCase);

        // 4. Remove 'to read more', 'read more', 'continue reading', 'click here to read more', etc.
        clean = Regex.Replace(clean, @"(?i)\b(to\s+read\s+more|read\s+more|continue\s+reading|click\s+here\s+to\s+read\s+more|read\s+full\s+story)\b.*$", string.Empty, RegexOptions.Multiline);
        clean = Regex.Replace(clean, @"(?i)\b(to\s+read\s+more|read\s+more|continue\s+reading|click\s+here\s+to\s+read\s+more|read\s+full\s+story)\b:?.*", string.Empty);

        // 5. Remove bracketed teasers and ellipsis indicators ([...], [&#8230;], etc.)
        clean = Regex.Replace(clean, @"\[.*?\]", string.Empty);

        // 6. Normalize multiple spaces/newlines to single space
        clean = Regex.Replace(clean, @"\s+", " ").Trim();

        // 7. Strip trailing stray punctuation often left by severed links (-, :, |, ,, ;)
        clean = clean.TrimEnd('-', ':', ',', ';', '|', ' ');

        return clean;
    }
}
