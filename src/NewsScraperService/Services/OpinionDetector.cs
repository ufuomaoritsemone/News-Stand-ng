namespace NewsScraperService.Services;

/// <summary>
/// High-precision rule-based opinion and editorial detector.
/// Identifies op-eds, columnists, viewpoints, and editorial board pieces across
/// major Nigerian newspapers via URL segments, source names, title prefixes, and section markers.
/// </summary>
public static class OpinionDetector
{
    private static readonly string[] OpinionUrlSegments =
    [
        "/opinion/",
        "/opinions/",
        "/editorial/",
        "/editorials/",
        "/column/",
        "/columns/",
        "/columnist/",
        "/columnists/",
        "/comment/",
        "/commentary/",
        "/analysis/",
        "/perspective/",
        "/viewpoint/",
        "/insight/",
        "/insights/",
        "/letters/",
        "/letters-to-editor/",
        "/oped/",
        "/op-ed/"
    ];

    private static readonly string[] OpinionTitlePrefixes =
    [
        "opinion:",
        "[opinion]",
        "editorial:",
        "[editorial]",
        "column:",
        "[column]",
        "commentary:",
        "[commentary]",
        "viewpoint:",
        "perspective:",
        "op-ed:",
        "[op-ed]",
        "letter to the editor:",
        "letters to the editor:"
    ];

    private static readonly string[] OpinionSectionKeywords =
    [
        "opinion",
        "editorial",
        "column",
        "columnist",
        "commentary",
        "viewpoint",
        "perspective"
    ];

    /// <summary>
    /// Evaluates article metadata to determine if it is an Editorial or Opinion piece.
    /// Returns "Opinion" if detected, or "News" by default.
    /// </summary>
    public static string Detect(string? url, string? title, string? category = null, string? sourceName = null)
    {
        // 1. Dedicated opinion sources (e.g. "Punch Opinions", "Vanguard Opinions")
        if (!string.IsNullOrWhiteSpace(sourceName) &&
            (sourceName.Contains("Opinion", StringComparison.OrdinalIgnoreCase) ||
             sourceName.Contains("Editorial", StringComparison.OrdinalIgnoreCase)))
        {
            return "Opinion";
        }

        // 2. Explicit URL paths
        if (!string.IsNullOrWhiteSpace(url))
        {
            foreach (var segment in OpinionUrlSegments)
            {
                if (url.Contains(segment, StringComparison.OrdinalIgnoreCase))
                {
                    return "Opinion";
                }
            }
        }

        // 3. Section or Category markers
        if (!string.IsNullOrWhiteSpace(category))
        {
            foreach (var keyword in OpinionSectionKeywords)
            {
                if (category.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return "Opinion";
                }
            }
        }

        // 4. Headline markers / prefixes
        if (!string.IsNullOrWhiteSpace(title))
        {
            var trimmedTitle = title.Trim();
            foreach (var prefix in OpinionTitlePrefixes)
            {
                if (trimmedTitle.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return "Opinion";
                }
            }
        }

        return "News";
    }
}
