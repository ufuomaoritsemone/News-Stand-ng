using System.Text.RegularExpressions;

namespace NewsApi.Services.Embeddings;

public partial class LocalFallbackEmbeddingProvider : IEmbeddingProvider
{
    public const int DefaultDimensions = 768;
    public int Dimensions => DefaultDimensions;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "about", "above", "after", "again", "against", "all", "am", "an", "and", "any", "are", "aren't",
        "as", "at", "be", "because", "been", "before", "being", "below", "between", "both", "but", "by", "can",
        "cannot", "could", "did", "do", "does", "doing", "down", "during", "each", "few", "for", "from", "further",
        "had", "has", "have", "having", "he", "her", "here", "hers", "herself", "him", "himself", "his", "how",
        "i", "if", "in", "into", "is", "it", "its", "itself", "let's", "me", "more", "most", "my", "myself",
        "no", "nor", "not", "of", "off", "on", "once", "only", "or", "other", "ought", "our", "ours", "ourselves",
        "out", "over", "own", "same", "she", "should", "so", "some", "such", "than", "that", "the", "their",
        "theirs", "them", "themselves", "then", "there", "these", "they", "this", "those", "through", "to", "too",
        "under", "until", "up", "very", "was", "we", "were", "what", "when", "where", "which", "while", "who",
        "whom", "why", "with", "would", "you", "your", "yours", "yourself", "yourselves"
    };

    // Canonical acronym and phrase expansions for Nigerian news domain
    private static readonly (Regex Pattern, string Replacement)[] CanonicalExpansions =
    [
        (new Regex(@"\bcentral\s+bank\s+of\s+nigeria\b|\bcbn\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "cbn central bank"),
        (new Regex(@"\bmonetary\s+policy\s+rate\b|\bmpr\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "mpr monetary policy rate"),
        (new Regex(@"\bnigerian\s+national\s+petroleum\b|\bnnpcl?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "nnpcl nnpc petrol"),
        (new Regex(@"\bpms\b|\bpremium\s+motor\s+spirit\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "petrol pms"),
        (new Regex(@"%", RegexOptions.Compiled), " percent "),
        (new Regex(@"\braises\b|\braised\b|\braising\b|\bincreases\b|\bincreased\b|\bincreasing\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "increase"),
        (new Regex(@"\bcombat\b|\bcombating\b|\bcombats\b|\bcombatted\b|\bcontrols\b|\bcontrolling\b|\bcontrolled\b|\bcurbs\b|\bcurbing\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "control"),
        (new Regex(@"\bstabilise\b|\bstabilises\b|\bstabilised\b|\bstabilising\b|\bstabilize\b|\bstabilizes\b|\bstabilized\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "stabilize"),
        (new Regex(@"\bbegins\b|\bbegun\b|\bbegan\b|\bbeginning\b|\bcommences\b|\bcommenced\b|\bcommencing\b|\bstarts\b|\bstarted\b|\bstarting\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "commence")
    ];

    [GeneratedRegex(@"[^\p{L}\p{Nd}\s]")]
    private static partial Regex NonAlphaNumericRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var vector = ComputeEmbedding(text);
        return Task.FromResult(vector);
    }

    public Task<List<float[]>> GenerateEmbeddingsBatchAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        var list = texts.Select(ComputeEmbedding).ToList();
        return Task.FromResult(list);
    }

    private float[] ComputeEmbedding(string text)
    {
        var vector = new float[Dimensions];
        if (string.IsNullOrWhiteSpace(text))
        {
            return vector;
        }

        var normalized = text.ToLowerInvariant();
        foreach (var (pattern, rep) in CanonicalExpansions)
        {
            normalized = pattern.Replace(normalized, rep);
        }

        // Clean and tokenize
        var clean = NonAlphaNumericRegex().Replace(normalized, " ");
        var rawTokens = WhitespaceRegex().Split(clean)
            .Where(t => t.Length >= 2 && !StopWords.Contains(t))
            .ToList();

        if (rawTokens.Count == 0)
        {
            return vector;
        }

        var tokens = rawTokens.Select(StemWord).ToList();

        // 1. Process unigrams with position weighting
        for (int i = 0; i < tokens.Count; i++)
        {
            var word = tokens[i];
            float posWeight = 1.0f + (1.2f / (1f + i * 0.12f));
            AddTokenFeatures(vector, word, posWeight);

            // Add 4-prefix feature for long words
            if (word.Length >= 5)
            {
                AddTokenFeatures(vector, word[..4], posWeight * 0.35f);
            }
        }

        // 2. Process bigrams for contextual phrase matching
        for (int i = 0; i < tokens.Count - 1; i++)
        {
            var bigram = $"{tokens[i]}_{tokens[i + 1]}";
            AddTokenFeatures(vector, bigram, 0.6f);
        }

        // Normalize vector to unit length
        VectorMath.Normalize(vector);
        return vector;
    }

    private static string StemWord(string word)
    {
        if (word.Length <= 4) return word;
        if (word.EndsWith("tion", StringComparison.Ordinal)) return word[..^4];
        if (word.EndsWith("ment", StringComparison.Ordinal)) return word[..^4];
        if (word.EndsWith("ing", StringComparison.Ordinal)) return word[..^3];
        if (word.EndsWith("ies", StringComparison.Ordinal)) return word[..^3] + "y";
        if (word.EndsWith("ed", StringComparison.Ordinal)) return word[..^2];
        if (word.EndsWith("es", StringComparison.Ordinal)) return word[..^2];
        if (word.EndsWith("s", StringComparison.Ordinal) && !word.EndsWith("ss", StringComparison.Ordinal)) return word[..^1];
        return word;
    }

    private static void AddTokenFeatures(Span<float> vector, string token, float weight)
    {
        int dim = vector.Length;
        uint h = HashMurmur3(token, 0x9747b28c);
        int idx = (int)(h % (uint)dim);
        float sign = (h & 0x80000000) == 0 ? 1f : -1f;

        vector[idx] += sign * weight;
    }

    private static uint HashMurmur3(string data, uint seed)
    {
        uint h = seed;
        for (int i = 0; i < data.Length; i++)
        {
            uint k = data[i];
            k *= 0xcc9e2d51;
            k = (k << 15) | (k >> 17);
            k *= 0x1b873593;

            h ^= k;
            h = (h << 13) | (h >> 19);
            h = h * 5 + 0xe6546b64;
        }

        h ^= (uint)data.Length;
        h ^= h >> 16;
        h *= 0x85ebca6b;
        h ^= h >> 13;
        h *= 0xc2b2ae35;
        h ^= h >> 16;

        return h;
    }
}
