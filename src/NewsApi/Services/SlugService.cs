using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace NewsApi.Services;

public partial class SlugService : ISlugService
{
    private static readonly char[] Base36Alphabet = "0123456789abcdefghijklmnopqrstuvwxyz".ToCharArray();

    [GeneratedRegex("<.*?>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"-+", RegexOptions.Compiled)]
    private static partial Regex MultipleHyphensRegex();

    public string GenerateSlug(string text, int maxLength = 80)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "item";
        }

        // Handle common Nigerian symbols
        var workingText = text.Replace("₦", "naira ", StringComparison.OrdinalIgnoreCase);

        // Normalize unicode accents to FormD to separate diacritics
        var normalized = workingText.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
            else if (char.IsWhiteSpace(c) || c is '-' or '_' or '/' or '\\' or ':' or '.')
            {
                sb.Append('-');
            }
        }

        var slug = sb.ToString();

        // Collapse multiple hyphens
        slug = MultipleHyphensRegex().Replace(slug, "-");
        slug = slug.Trim('-');

        if (string.IsNullOrWhiteSpace(slug))
        {
            return "item";
        }

        if (slug.Length > maxLength)
        {
            slug = slug[..maxLength].TrimEnd('-');
        }

        return string.IsNullOrEmpty(slug) ? "item" : slug;
    }

    public string GenerateArticleSlug(string title, string? id = null, int maxLength = 80)
    {
        // 6 characters reserved for hash + 1 for hyphen delimiter
        const int suffixLength = 7;
        var maxBaseLength = Math.Max(10, maxLength - suffixLength);

        var baseSlug = GenerateSlug(title, maxBaseLength);
        var hash = GenerateSuffixHash(id);

        return $"{baseSlug}-{hash}";
    }

    public string GenerateExcerpt(string? content, string? summary, int maxChars = 300)
    {
        var rawSource = !string.IsNullOrWhiteSpace(summary) ? summary : content;
        if (string.IsNullOrWhiteSpace(rawSource))
        {
            return string.Empty;
        }

        // Clean HTML tags and decode entities
        var clean = HtmlTagRegex().Replace(rawSource, " ");
        clean = WebUtility.HtmlDecode(clean);
        clean = WhitespaceRegex().Replace(clean, " ").Trim();

        if (clean.Length <= maxChars)
        {
            return clean;
        }

        // Look for clean sentence break within the target character window
        var searchWindow = clean[..maxChars];
        var sentenceEndIdx = Math.Max(
            searchWindow.LastIndexOf(". ", StringComparison.Ordinal),
            Math.Max(
                searchWindow.LastIndexOf("! ", StringComparison.Ordinal),
                searchWindow.LastIndexOf("? ", StringComparison.Ordinal)
            )
        );

        // If a sentence break exists in the latter half of the window, cut cleanly on sentence
        if (sentenceEndIdx >= maxChars * 0.45)
        {
            return clean[..(sentenceEndIdx + 1)].Trim();
        }

        // Otherwise break cleanly on the last whitespace
        var lastSpace = searchWindow.LastIndexOf(' ');
        if (lastSpace > 0)
        {
            return $"{clean[..lastSpace].Trim()}...";
        }

        return $"{clean[..(maxChars - 3)].Trim()}...";
    }

    private static string GenerateSuffixHash(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            // Use deterministic 6-character hash from ID if available
            using var sha = SHA256.Create();
            var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(id.Trim()));
            var sb = new StringBuilder(6);
            for (int i = 0; i < 6; i++)
            {
                sb.Append(Base36Alphabet[hashBytes[i] % 36]);
            }
            return sb.ToString();
        }

        // Random 6-character base36 hash
        var randomBytes = new byte[6];
        RandomNumberGenerator.Fill(randomBytes);
        var randomSb = new StringBuilder(6);
        for (int i = 0; i < 6; i++)
        {
            randomSb.Append(Base36Alphabet[randomBytes[i] % 36]);
        }
        return randomSb.ToString();
    }
}
