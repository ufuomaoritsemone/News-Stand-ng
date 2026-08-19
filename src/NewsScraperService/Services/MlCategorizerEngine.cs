using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace NewsScraperService.Services;

public class ArticleInputData
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class ArticlePredictionOutput
{
    [ColumnName("PredictedLabel")]
    public string PredictedCategory { get; set; } = string.Empty;

    public float[] Score { get; set; } = Array.Empty<float>();
}

public class MlCategorizerEngine
{
    private readonly ILogger<MlCategorizerEngine> _logger;
    private readonly PredictionEngine<ArticleInputData, ArticlePredictionOutput>? _predictionEngine;
    private readonly object _lock = new();

    private const float ConfidenceThreshold = 0.40f;

    // High-Precision Compiled Regex Patterns with strict word boundaries
    private static readonly Regex SportsRegex = new(
        @"\b(super eagles|super falcons|afcon|fifa|caf|epl|premier league|champions league|europa league|la liga|serie a|bundesliga|npfl|fa cup|ballon d'or|olympic|osimhen|lookman|boniface|arsenal|chelsea|manchester united|man united|man city|liverpool|real madrid|barcelona|galatasaray|atalanta|nwabali|amusan|joshua|boxer|boxing|bout|knockout|heavyweight|goalscorer|hat-trick|striker|midfielder|winger|goalkeeper|referee|head coach|transfer window|transfer fee|clean sheet|d'tigress|d'tigers|table tennis|aruna|quadri)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PoliticsRegex = new(
        @"\b(tinubu|bola tinubu|shettima|atiku|peter obi|kwankwaso|wike|sanwo-olu|abiodun|makinde|adeleke|soludo|alex otti|peter mbah|zulum|akpabio|godswill akpabio|barau jibrin|tajudeen abbas|gbajabiamila|apc|pdp|labour party|\blp\b|nnpp|apga|inec|senate|national assembly|house of representatives|house of reps|presidency|aso rock|federal executive council|\bfec\b|governor|governors|governorship|gubernatorial|deputy governor|commissioner|commissioners|local government autonomy|electoral act|electoral bill|election appeal|tribunal|by-election|primary election|peace accord|defected|defection|impeachment|cabinet reshuffle|minister of|special adviser|state budget|appropriation bill)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BusinessRegex = new(
        @"\b(cbn|central bank of nigeria|olayemi cardoso|monetary policy|all-share index|\bngx\b|market capitalization|naira|forex|foreign exchange|nafem|us dollar|fx market|headline inflation|food inflation|consumer price index|\bnbs\b|gross domestic product|\bgdp\b|dmo|debt management|treasury bills|fgn bonds|dangote|refinery|nnpc|nuprc|nmdpra|crude oil production|barrels per day|\bbpd\b|opec|pms price|pms petrol|fuel subsidy|afcfta|firs|company income tax|tier-1 banks|zenith bank|gtbank|access holdings|first bank|fidelity bank|dividend payout|shareholders|recapitalization|rights issue|profit after tax|world bank|financing package|\bimf\b|customs revenue)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CrimeRegex = new(
        @"\b(efcc|economic and financial crimes|icpc|ndlea|police command|commissioner of police|\bigp\b|kayode egbetokun|dss|department of state services|civil defence|nscdc|kidnapping|kidnappers|kidnapper|abducted|abduction|hostage|ransom|bandits|banditry|armed robbery|robbers|burglary|cult clash|cultism|cyber crime|cybercrime|yahoo boys|advance fee fraud|wire fraud|money laundering|embezzlement|arms trafficking|illegal firearms|ammunition seized|drug trafficking|cocaine seizure|cannabis plantation|human trafficking|ritual killing|oil bunkering|illegal refinery|pirates|piracy|arraigned|docked|remanded in custody|convicted|sentenced to imprisonment|prosecution witness|terrorists neutralized|iswap|boko haram|counter-terrorism)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EntertainmentRegex = new(
        @"\b(burna boy|wizkid|davido|asake|rema|tems|ayra starr|tiwa savage|olamide|fireboy|kizz daniel|shallipopi|odumodublvck|phyno|flavour|simi|adekunle gold|amvca|africa magic viewers|grammy awards|grammy nomination|headies|billboard|spotify streams|apple music chart|o2 arena|sold-out concert|album release|nollywood|funke akindele|kunle afolayan|genevieve nnaji|richard mofe-damijo|\brmd\b|femi adebayo|sola sobowale|box office record|highest grossing movie|cinema release|bbnaija|big brother naija|housemates|eviction night|reality tv show|fashion week)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TechnologyRegex = new(
        @"\b(fintech|startup|series [a-d]|seed funding|venture capital|\bvc\b|flutterwave|paystack|moniepoint|opay|piggyvest|kuda bank|andela|interswitch|chowdeck|moove|\bai\b|artificial intelligence|machine learning|large language model|generative ai|cloud computing|azure|google cloud|\baws\b|data center|starlink|satellite internet|\b5g\b|broadband|telecoms|\bnitda\b|\bncc\b|cybersecurity|software engineering|developer ecosystem|app store|play store|mobile app|web3|blockchain|crypto exchange)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex GeneralRegex = new(
        @"\b(frsc|road safety|road crash|fatal accident|highway collision|cholera outbreak|lassa fever|ncdc|yellow fever|immunization|public health|teaching hospital|ministry of education|\bwaec\b|\bjamb\b|\butme\b|\bneco\b|\basuu\b|nelfund|student loan|nema|flood disaster|earth tremor|seismic|weather forecast|nimet|rainstorm|nlc|tuc|warning strike|minimum wage|salary arrears|christian association of nigeria|catholic bishops|nscia|sultan of sokoto|ooni of ife|traditional ruler|public holiday|united nations|\bun\b|visas? category|visa categories|visa application|embassy)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public MlCategorizerEngine(ILogger<MlCategorizerEngine> logger)
    {
        _logger = logger;

        try
        {
            string baseDir = AppContext.BaseDirectory;
            var searchPaths = new[]
            {
                Path.Combine(baseDir, "Models", "categorizer_model.zip"),
                Path.GetFullPath(Path.Combine(baseDir, "../../../Models/categorizer_model.zip")),
                Path.GetFullPath(Path.Combine(baseDir, "../../../../src/NewsScraperService/Models/categorizer_model.zip")),
                Path.GetFullPath(Path.Combine(baseDir, "../../../../../src/NewsScraperService/Models/categorizer_model.zip"))
            };

            string? foundPath = searchPaths.FirstOrDefault(File.Exists);

            if (foundPath != null)
            {
                var mlContext = new MLContext();
                ITransformer model = mlContext.Model.Load(foundPath, out _);
                _predictionEngine = mlContext.Model.CreatePredictionEngine<ArticleInputData, ArticlePredictionOutput>(model);
                _logger.LogInformation("Successfully loaded ML.NET categorizer model from {ModelPath}", foundPath);
            }
            else
            {
                _logger.LogWarning("ML.NET model file not found in search paths. Categorizer will use high-precision rule-based engine.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize ML.NET prediction engine. Falling back to high-precision rule-based categorizer.");
        }
    }

    public string Categorize(string title, string? summary = null)
    {
        var (category, _, _) = CategorizeWithDetails(title, summary);
        return category;
    }

    public (string Category, float Confidence, string Method) CategorizeWithDetails(string title, string? summary = null)
    {
        string cleanTitle = CleanNewsText(title);
        string cleanSummary = CleanNewsText(summary ?? string.Empty);

        if (string.IsNullOrWhiteSpace(cleanTitle) && string.IsNullOrWhiteSpace(cleanSummary))
        {
            return ("General", 1.0f, "EmptyTextDefault");
        }

        // 1. Evaluate with ML.NET Prediction Engine if loaded
        if (_predictionEngine != null)
        {
            try
            {
                lock (_lock)
                {
                    // 3x Title weighting to prioritize headline discriminative tokens
                    var input = new ArticleInputData
                    {
                        Title = cleanTitle,
                        Summary = cleanSummary,
                        Text = $"{cleanTitle} {cleanTitle} {cleanTitle} {cleanSummary}".Trim(),
                        Category = string.Empty
                    };

                    var prediction = _predictionEngine.Predict(input);
                    float maxScore = prediction.Score != null && prediction.Score.Length > 0 ? prediction.Score.Max() : 0f;

                    if (!string.IsNullOrWhiteSpace(prediction.PredictedCategory) && maxScore >= ConfidenceThreshold)
                    {
                        return (prediction.PredictedCategory, maxScore, "ML_HighConfidence");
                    }

                    // If ML confidence is below threshold, check if rule engine has a high-confidence match
                    var ruleResult = CategorizeByLexicon(cleanTitle, cleanSummary);
                    if (ruleResult.Confidence > 0.6f)
                    {
                        return (ruleResult.Category, ruleResult.Confidence, "Rule_OverrideLowML");
                    }

                    // Otherwise return the ML prediction if available, or rule fallback
                    if (!string.IsNullOrWhiteSpace(prediction.PredictedCategory))
                    {
                        return (prediction.PredictedCategory, maxScore, "ML_LowConfidence");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error during ML.NET category prediction: {Message}. Using rule-based fallback.", ex.Message);
            }
        }

        // 2. High-Precision Lexicon / Entity Fallback
        var fallback = CategorizeByLexicon(cleanTitle, cleanSummary);
        return (fallback.Category, fallback.Confidence, "Rule_Lexicon");
    }

    public static (string Category, float Confidence) CategorizeByLexicon(string title, string summary)
    {
        string fullText = $"{title} {summary}".Trim();
        if (string.IsNullOrWhiteSpace(fullText)) return ("General", 0.5f);

        // Calculate weighted keyword match scores (Title matches carry 3x weight)
        var scores = new Dictionary<string, int>
        {
            { "Sports", CountMatches(SportsRegex, title) * 3 + CountMatches(SportsRegex, summary) },
            { "Politics", CountMatches(PoliticsRegex, title) * 3 + CountMatches(PoliticsRegex, summary) },
            { "Business", CountMatches(BusinessRegex, title) * 3 + CountMatches(BusinessRegex, summary) },
            { "Crime", CountMatches(CrimeRegex, title) * 3 + CountMatches(CrimeRegex, summary) },
            { "Entertainment", CountMatches(EntertainmentRegex, title) * 3 + CountMatches(EntertainmentRegex, summary) },
            { "Technology", CountMatches(TechnologyRegex, title) * 3 + CountMatches(TechnologyRegex, summary) },
            { "General", CountMatches(GeneralRegex, title) * 3 + CountMatches(GeneralRegex, summary) }
        };

        var bestMatch = scores.OrderByDescending(kv => kv.Value).First();

        if (bestMatch.Value > 0)
        {
            float confidence = Math.Min(0.95f, 0.5f + (bestMatch.Value * 0.15f));
            return (bestMatch.Key, confidence);
        }

        return ("General", 0.5f);
    }

    public static string CategorizeFallback(string text)
    {
        var clean = CleanNewsText(text);
        var (category, _) = CategorizeByLexicon(clean, string.Empty);
        return category;
    }

    private static int CountMatches(Regex regex, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return regex.Matches(text).Count;
    }

    public static string CleanNewsText(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;

        // Strip HTML tags
        string clean = Regex.Replace(rawText, "<.*?>", " ");

        // Decode HTML entities
        clean = System.Net.WebUtility.HtmlDecode(clean);

        // Strip URL patterns & query params
        clean = Regex.Replace(clean, @"https?://\S+", " ");
        clean = Regex.Replace(clean, @"www\.\S+", " ");
        clean = Regex.Replace(clean, @"\b\w+\.(com|ng|org|net|co|io|africa)\b", " ", RegexOptions.IgnoreCase);

        // Strip common boilerplate markers
        clean = Regex.Replace(clean, @"Read More:.*", " ", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"The post .* appeared first on .*", " ", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"\(CREDIT:.*?\)", " ", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"\[&#8230;\]", " ", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"\[\.\.\.\]", " ", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"\b(utm_source|utm_medium|utm_campaign|rss|web)\b", " ", RegexOptions.IgnoreCase);

        // Normalize whitespace
        clean = Regex.Replace(clean, @"\s+", " ").Trim();

        return clean;
    }
}
