using System.Net;
using System.Text;

namespace NigerianNewsGrid.Client.Helpers;

/// <summary>
/// Helper for formatting and constructing 𝕏 (formerly Twitter) share payloads,
/// ensuring character limits (280 characters max, 23 chars per URL) are adhered to,
/// reputable Nigerian news publisher handles are attributed, and app download CTAs are embedded.
/// </summary>
public static class XShareFormatter
{
    public const string DefaultAppStoreUrl = AppStoreConstants.GooglePlayStoreUrl;

    public const string DefaultAppCta = "📲 Get News Stand NG on Play Store:";

    /// <summary>
    /// Maps known Nigerian and international news publishers to their official 𝕏 (@username) handles.
    /// </summary>
    public static string MapSourceToHandle(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return "News Stand NG";

        var clean = source.Trim();

        return clean.ToLowerInvariant() switch
        {
            "punch" or "the punch" or "punch newspapers" => "@MobilePunch",
            "vanguard" or "vanguard news" or "vanguardngr" => "@vanguardngrnews",
            "channels" or "channels tv" or "channels television" => "@channelstv",
            "guardian" or "the guardian" or "the guardian nigeria" => "@GuardianNigeria",
            "premium times" or "premiumtimes" => "@PremiumTimesng",
            "thecable" or "the cable" => "@thecableng",
            "daily post" or "dailypost" or "daily post nigeria" => "@DailyPostNGR",
            "leadership" or "leadership news" => "@LeadershipNGA",
            "businessday" or "business day" => "@BusinessDayNg",
            "sahara reporters" or "saharareporters" => "@SaharaReporters",
            "arise" or "arise news" or "arise tv" => "@ARISEtv",
            "tribune" or "nigerian tribune" => "@nigeriantribune",
            "thisday" or "this day" or "thisday live" => "@ThisdayLive",
            "the nation" or "nation" or "the nation newspaper" => "@TheNationNews",
            "sun" or "the sun" or "the sun nigeria" => "@thesunnigeria",
            "blueprint" or "blueprint newspaper" => "@BlueprintNg",
            _ => clean.StartsWith('@') ? clean : $"via {clean}"
        };
    }

    /// <summary>
    /// Composes a tweet message tailored for 𝕏, keeping total length within 280 characters.
    /// If <paramref name="appDownloadUrl"/> is empty or null, the download CTA is omitted.
    /// Note: 𝕏 treats all URLs as 23 characters (t.co wrapper).
    /// </summary>
    public static string BuildTweetText(
        string title,
        string? source,
        string? appDownloadUrl = DefaultAppStoreUrl,
        string? shareUrl = null)
    {
        var handle = MapSourceToHandle(source);
        var sourceText = handle.StartsWith('@') ? $"Via {handle}" : handle;
        var hasAppDownload = !string.IsNullOrWhiteSpace(appDownloadUrl);
        var ctaPrefix = DefaultAppCta;

        // Ensure total raw string length including the URL string does not exceed 280 characters,
        // which also guarantees 𝕏's 280-weighted limit is never exceeded.
        int rawUrlLength = hasAppDownload ? appDownloadUrl!.Length : 0;
        int shareUrlBudget = !string.IsNullOrWhiteSpace(shareUrl) ? 25 : 0;
        int reservedChars = sourceText.Length + (hasAppDownload ? ctaPrefix.Length + rawUrlLength + 6 : 4) + shareUrlBudget;
        int maxTitleLength = Math.Clamp(280 - reservedChars, 50, 220);

        var trimmedTitle = title?.Trim() ?? string.Empty;
        if (trimmedTitle.Length > maxTitleLength)
        {
            trimmedTitle = $"{trimmedTitle[..(maxTitleLength - 3)].TrimEnd()}...";
        }

        var sb = new StringBuilder();
        sb.AppendLine(trimmedTitle);
        sb.AppendLine();
        sb.Append(sourceText);

        if (hasAppDownload)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.Append($"{ctaPrefix} {appDownloadUrl}");
        }

        var result = sb.ToString().Trim();
        if (result.Length > 280)
        {
            int overflow = result.Length - 280;
            if (trimmedTitle.Length > overflow + 3)
            {
                trimmedTitle = $"{trimmedTitle[..(trimmedTitle.Length - overflow - 3)].TrimEnd()}...";
                sb.Clear();
                sb.AppendLine(trimmedTitle);
                sb.AppendLine();
                sb.Append(sourceText);
                if (hasAppDownload)
                {
                    sb.AppendLine();
                    sb.AppendLine();
                    sb.Append($"{ctaPrefix} {appDownloadUrl}");
                }
                result = sb.ToString().Trim();
            }
        }

        return result;
    }

    /// <summary>
    /// Builds the web intent URL (https://x.com/intent/tweet?text=...&url=...).
    /// </summary>
    public static Uri BuildWebIntentUri(string tweetText, string? shareUrl = null)
    {
        var encodedText = WebUtility.UrlEncode(tweetText);
        if (!string.IsNullOrWhiteSpace(shareUrl))
        {
            var encodedUrl = WebUtility.UrlEncode(shareUrl);
            return new Uri($"https://x.com/intent/tweet?text={encodedText}&url={encodedUrl}");
        }

        return new Uri($"https://x.com/intent/tweet?text={encodedText}");
    }

    /// <summary>
    /// Builds the native 𝕏 app intent URI (twitter://post?message=...).
    /// </summary>
    public static Uri BuildNativeIntentUri(string tweetText, string? shareUrl = null)
    {
        var combined = string.IsNullOrWhiteSpace(shareUrl)
            ? tweetText
            : $"{tweetText}\n\n{shareUrl}".Trim();

        var encoded = WebUtility.UrlEncode(combined);
        return new Uri($"twitter://post?message={encoded}");
    }
}
