using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NigerianNewsGrid.Client.Helpers;

/// <summary>
/// Architecture Component: Cross-Source News Story Deduplication Engine.
/// 
/// Identifies and eliminates redundant coverage of the same news story reported by different
/// news outlets (e.g. Punch, Vanguard, TheCable, Daily Post, Premium Times) prior to Audio TTS synthesis
/// and daily briefing delivery.
/// </summary>
public static class StoryDeduplicationHelper
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "about", "above", "across", "after", "again", "against", "all", "almost", "along", "also",
        "am", "an", "and", "any", "are", "aren't", "as", "at", "be", "because", "been", "before", "being",
        "below", "between", "both", "but", "by", "can", "can't", "cannot", "could", "did", "didn't",
        "do", "does", "doesn't", "doing", "don't", "down", "during", "each", "few", "for", "from",
        "further", "had", "hadn't", "has", "hasn't", "have", "haven't", "having", "he", "her", "here",
        "hers", "herself", "him", "himself", "his", "how", "i", "if", "in", "into", "is", "isn't",
        "it", "it's", "its", "itself", "let's", "me", "more", "most", "must", "my", "myself", "no",
        "nor", "not", "of", "off", "on", "once", "only", "or", "other", "ought", "our", "ours", "out",
        "over", "own", "same", "she", "should", "shouldn't", "so", "some", "such", "than", "that",
        "the", "their", "theirs", "them", "themselves", "then", "there", "these", "they", "this",
        "those", "through", "to", "too", "under", "until", "up", "very", "was", "wasn't", "we", "were",
        "weren't", "what", "when", "where", "which", "while", "who", "whom", "why", "with", "won't",
        "would", "you", "your", "yours", "yourself", "yourselves"
    };

    private static readonly HashSet<string> ReportingBuzzwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "says", "said", "say", "tells", "told", "warns", "warned", "declares", "declared", "urges", "urged",
        "claims", "claimed", "speaks", "ahead", "latest", "today", "yesterday", "tomorrow", "this", "month",
        "year", "news", "report", "reports", "reported", "breaking", "update", "exclusive", "watch", "photos",
        "video", "photos/video", "read", "full", "story", "details", "emerge", "emerges", "reveals", "revealed",
        "just", "in", "nigeria", "nigerian"
    };

    private static readonly Regex BrandSuffixPattern = new(
        @"\s*[-|–—:]\s*(Punch(\s*Newspapers?)?|Vanguard(\s*News?)?|TheCable|Daily\s*Post(\s*Nigeria)?|Premium\s*Times|Sahara\s*Reporters|The\s*Guardian(\s*Nigeria)?|Leadership(\s*News?)?|ThisDay(\s*Live)?|Daily\s*Trust|Channels(\s*Television|\s*TV)?|Nairametrics|Tribune(\s*Online)?|Arise\s*News)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EditorialPrefixPattern = new(
        @"^\s*(\[?(BREAKING(\s*NEWS)?|JUST\s*IN|UPDATE|EXCLUSIVE|WATCH|PHOTOS?|VIDEO|SPECIAL\s*REPORT|OPINION|EDITORIAL)\]?\s*[:\-\|]\s*)+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NonAlphanumericPattern = new(
        @"[^\w\s]",
        RegexOptions.Compiled);

    /// <summary>
    /// Evaluates whether two news stories represent duplicate reporting of the same event
    /// from the same or different news sources.
    /// </summary>
    public static bool AreDuplicateStories(
        string? title1,
        string? title2,
        string? summary1 = null,
        string? summary2 = null)
    {
        if (string.IsNullOrWhiteSpace(title1) || string.IsNullOrWhiteSpace(title2))
            return false;

        var norm1 = NormalizeTitle(title1);
        var norm2 = NormalizeTitle(title2);

        // 1. Exact or near-exact match after normalization
        if (string.Equals(norm1, norm2, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(norm1) && !string.IsNullOrWhiteSpace(norm2))
        {
            // Substring containment with high length proportion (e.g. one outlet truncated the other)
            if (norm1.Contains(norm2, StringComparison.OrdinalIgnoreCase) &&
                (double)norm2.Length / norm1.Length >= 0.65)
                return true;

            if (norm2.Contains(norm1, StringComparison.OrdinalIgnoreCase) &&
                (double)norm1.Length / norm2.Length >= 0.65)
                return true;
        }

        // 2. Tokenize and extract significant stemmed tokens
        var tokens1 = ExtractSignificantTokens(title1);
        var tokens2 = ExtractSignificantTokens(title2);

        if (tokens1.Count == 0 || tokens2.Count == 0)
            return false;

        int intersectionCount = tokens1.Intersect(tokens2).Count();
        int minCount = Math.Min(tokens1.Count, tokens2.Count);
        int totalTokens = tokens1.Count + tokens2.Count;
        int unionCount = tokens1.Union(tokens2).Count();

        double overlapCoeff = (double)intersectionCount / minCount;
        double jaccard = unionCount > 0 ? (double)intersectionCount / unionCount : 0.0;
        double dice = totalTokens > 0 ? (2.0 * intersectionCount) / totalTokens : 0.0;

        // High overlap criteria
        // (A) 4 or more significant keyword matches and >= 58% overlap of smaller headline
        if (intersectionCount >= 4 && overlapCoeff >= 0.58)
            return true;

        // (B) 3 keyword matches with high overlap (>= 65%) and strong Dice score (>= 0.5)
        if (intersectionCount >= 3 && overlapCoeff >= 0.65 && dice >= 0.50)
            return true;

        // (C) Strong Jaccard or Dice index
        if (jaccard >= 0.45 || dice >= 0.60)
            return true;

        // 3. Fallback check: If titles have moderate overlap (>= 2 keywords, >= 40% overlap),
        // check whether summaries corroborate that it is the exact same event
        if (intersectionCount >= 2 && overlapCoeff >= 0.40 &&
            !string.IsNullOrWhiteSpace(summary1) && !string.IsNullOrWhiteSpace(summary2))
        {
            var summaryTokens1 = ExtractSignificantTokens(summary1);
            var summaryTokens2 = ExtractSignificantTokens(summary2);
            if (summaryTokens1.Count >= 3 && summaryTokens2.Count >= 3)
            {
                int sumIntersection = summaryTokens1.Intersect(summaryTokens2).Count();
                int sumMin = Math.Min(summaryTokens1.Count, summaryTokens2.Count);
                if (sumIntersection >= 3 && ((double)sumIntersection / sumMin) >= 0.45)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Filters an incoming sequence of news items, removing subsequent duplicates of any story
    /// that has already been accepted into the result list.
    /// Preserves original ordering (e.g. publication time or ranking).
    /// </summary>
    public static IEnumerable<T> DeduplicateStories<T>(
        IEnumerable<T> items,
        Func<T, string?> titleSelector,
        Func<T, string?>? summarySelector = null,
        Func<T, string?>? sourceSelector = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(titleSelector);

        var accepted = new List<(string Title, string? Summary, string? Source)>();

        foreach (var item in items)
        {
            if (item == null) continue;

            var title = titleSelector(item);
            if (string.IsNullOrWhiteSpace(title)) continue;

            var summary = summarySelector?.Invoke(item);
            var source = sourceSelector?.Invoke(item);

            bool isDuplicate = false;
            foreach (var existing in accepted)
            {
                if (AreDuplicateStories(title, existing.Title, summary, existing.Summary))
                {
                    isDuplicate = true;
                    break;
                }
            }

            if (!isDuplicate)
            {
                accepted.Add((title, summary, source));
                yield return item;
            }
        }
    }

    /// <summary>
    /// Strips branding suffixes, editorial tags, HTML, and punctuation, normalizing the headline for comparison.
    /// </summary>
    public static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        // Clean teaser links, URLs, and HTML entities
        var clean = TtsBriefingFormatter.CleanTextForTts(title);

        // Strip editorial prefixes ("BREAKING: ", "JUST IN: ")
        clean = EditorialPrefixPattern.Replace(clean, string.Empty);

        // Strip publisher attribution suffixes (" - Punch Newspapers", " | Vanguard")
        clean = BrandSuffixPattern.Replace(clean, string.Empty);

        // Remove punctuation and normalize spaces
        clean = NonAlphanumericPattern.Replace(clean, " ");
        clean = Regex.Replace(clean, @"\s+", " ").Trim();

        return clean.ToLowerInvariant();
    }

    private static readonly Dictionary<string, string> CanonicalSynonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        // Action verbs
        ["directs"] = "order",
        ["directed"] = "order",
        ["directing"] = "order",
        ["orders"] = "order",
        ["ordered"] = "order",
        ["ordering"] = "order",

        ["commence"] = "begin",
        ["commences"] = "begin",
        ["commenced"] = "begin",
        ["commencing"] = "begin",
        ["begins"] = "begin",
        ["began"] = "begin",
        ["beginning"] = "begin",
        ["start"] = "begin",
        ["starts"] = "begin",
        ["started"] = "begin",
        ["starting"] = "begin",

        ["slashes"] = "cut",
        ["slashed"] = "cut",
        ["slashing"] = "cut",
        ["reduces"] = "cut",
        ["reduced"] = "cut",
        ["reducing"] = "cut",
        ["reduction"] = "cut",
        ["cuts"] = "cut",
        ["cutting"] = "cut",

        ["withdraws"] = "drop",
        ["withdrawn"] = "drop",
        ["withdrawing"] = "drop",
        ["withdrawal"] = "drop",
        ["drops"] = "drop",
        ["dropped"] = "drop",
        ["dropping"] = "drop",

        ["kill"] = "kill",
        ["kills"] = "kill",
        ["killed"] = "kill",
        ["killing"] = "kill",
        ["slay"] = "kill",
        ["slain"] = "kill",

        ["abduct"] = "abduct",
        ["abducts"] = "abduct",
        ["abducted"] = "abduct",
        ["abducting"] = "abduct",
        ["kidnap"] = "abduct",
        ["kidnaps"] = "abduct",
        ["kidnapped"] = "abduct",
        ["kidnapping"] = "abduct",

        // Sports terms & nicknames
        ["spurs"] = "tottenham",
        ["gunners"] = "arsenal",
        ["reds"] = "liverpool",
        ["blues"] = "chelsea",
        ["brace"] = "twice",
        ["nets"] = "score",
        ["netted"] = "score",
        ["scores"] = "score",
        ["scored"] = "score",
        ["scoring"] = "score",
        ["beats"] = "defeat",
        ["beat"] = "defeat",
        ["defeats"] = "defeat",
        ["defeated"] = "defeat",
        ["defeating"] = "defeat",
        ["win"] = "defeat",
        ["wins"] = "defeat",
        ["won"] = "defeat",
        ["triumph"] = "defeat",
        ["triumphed"] = "defeat",
        ["victory"] = "defeat"
    };

    /// <summary>
    /// Extracts a set of stemmed, significant content tokens from text (excluding stop words and reporting noise).
    /// </summary>
    public static HashSet<string> ExtractSignificantTokens(string text)
    {
        var normalized = NormalizeTitle(text);
        if (string.IsNullOrWhiteSpace(normalized))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var word in words)
        {
            if (word.Length < 2) continue;
            if (StopWords.Contains(word)) continue;
            if (ReportingBuzzwords.Contains(word)) continue;

            var stemmed = StemWord(word);
            if (CanonicalSynonyms.TryGetValue(stemmed, out var canonical))
            {
                stemmed = canonical;
            }

            if (stemmed.Length >= 2 && !StopWords.Contains(stemmed) && !ReportingBuzzwords.Contains(stemmed))
            {
                tokens.Add(stemmed);
            }
        }

        return tokens;
    }

    /// <summary>
    /// Performs lightweight algorithmic inflection stemming for news tokens
    /// (e.g. 'protesters' -> 'protest', 'killed' -> 'kill', 'orders' -> 'order').
    /// </summary>
    public static string StemWord(string word)
    {
        if (word.Length <= 3)
            return word;

        if (CanonicalSynonyms.TryGetValue(word, out var canonical))
            return canonical;

        // "protesters" -> "protest", "workers" -> "work"
        if (word.EndsWith("ers", StringComparison.OrdinalIgnoreCase) && word.Length > 5)
            return word[..^3];

        // "protester" -> "protest", "leader" -> "lead"
        if (word.EndsWith("er", StringComparison.OrdinalIgnoreCase) && word.Length > 4)
            return word[..^2];

        // "ministries" -> "ministry"
        if (word.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && word.Length > 5)
            return word[..^3] + "y";

        // "protesting" -> "protest", "killing" -> "kill"
        if (word.EndsWith("ing", StringComparison.OrdinalIgnoreCase) && word.Length > 5)
        {
            var baseWord = word[..^3];
            // Only collapse double consonant if it is one of the standard doubled consonants
            if (baseWord.Length >= 4 && baseWord[^1] == baseWord[^2] && "bdgmnprt".Contains(char.ToLowerInvariant(baseWord[^1])))
                return baseWord[..^1];
            return baseWord;
        }

        // "ordered" -> "order", "killed" -> "kill"
        if (word.EndsWith("ed", StringComparison.OrdinalIgnoreCase) && word.Length > 4)
        {
            var baseWord = word[..^2];
            if (baseWord.Length >= 4 && baseWord[^1] == baseWord[^2] && "bdgmnprt".Contains(char.ToLowerInvariant(baseWord[^1])))
                return baseWord[..^1];
            return baseWord;
        }

        // "attacks" -> "attack", "protests" -> "protest"
        if (word.EndsWith("es", StringComparison.OrdinalIgnoreCase) && word.Length > 4)
            return word[..^2];

        // "minors" -> "minor", "leaders" -> "leader" (avoid "business", "grass")
        if (word.EndsWith('s') && !word.EndsWith("ss", StringComparison.OrdinalIgnoreCase) && word.Length > 3)
            return word[..^1];

        return word;
    }
}
