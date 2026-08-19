using System.ServiceModel.Syndication;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Text;

namespace NewsCategorizer.Trainer;

class Program
{
    private static readonly string DatasetPath = Path.Combine(AppContext.BaseDirectory, "dataset.json");
    private static readonly string ModelOutputPath = Path.Combine(AppContext.BaseDirectory, "categorizer_model.zip");

    // Common news boilerplate words to remove as stopwords
    private static readonly string[] CustomStopWords = new[]
    {
        "said", "says", "according", "disclosed", "statement", "told", "news", "report", "reported",
        "read", "more", "post", "appeared", "first", "click", "photo", "image", "video", "via",
        "http", "https", "www", "com", "ng", "org", "net", "html", "featured", "author",
        "correspondent", "yesterday", "today", "tomorrow", "also", "one", "two", "three",
        "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday",
        "state", "nigeria", "nigerian", "lagos", "abuja", "dailypost", "punchng", "vanguardngr",
        "guardian", "premiumtimes", "dailytrust", "readmore", "subscribe", "exclusive"
    };

    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine("   Nigerian News Grid - ML.NET Categorizer Trainer");
        Console.WriteLine("=================================================");

        string command = args.Length > 0 ? args[0].ToLowerInvariant() : "all";

        switch (command)
        {
            case "--scrape":
            case "scrape":
                await ScrapeDatasetAsync();
                break;

            case "--train":
            case "train":
                TrainModel();
                break;

            case "--predict":
            case "predict":
                string sample = args.Length > 1 ? string.Join(" ", args.Skip(1)) : "Tinubu approves new minimum wage for civil servants in Abuja";
                PredictSample(sample);
                break;

            case "--all":
            case "all":
            default:
                await ScrapeDatasetAsync();
                TrainModel();
                Console.WriteLine("\n--- Testing Sample Predictions across All 7 Categories ---");
                PredictSample("Super Eagles striker Osimhen scores stunning hat-trick in AFCON qualifier match");
                PredictSample("CBN increases monetary policy rate by 50 basis points to curb inflation");
                PredictSample("EFCC arrests 45 fraud suspects in Lagos cyber crime operation");
                PredictSample("Burna Boy and Wizkid perform sold out historic concert at London O2 arena");
                PredictSample("Fintech startup Moniepoint raises 110 million dollars in Series C funding");
                PredictSample("President Bola Tinubu signs new tax reform executive order at Aso Rock");
                PredictSample("FRSC confirms 15 dead in tragic head-on highway collision in Niger State");
                PredictSample("Earth tremor in Abuja: Minister Dele Alake urges calm, orders seismic updates");
                PredictSample("Five US work visa categories and application requirements for skilled immigrants");
                PredictSample("British boxer Raven Chapman recovering after emergency brain surgery");
                PredictSample("Supreme Court reserves judgment in Osun State governorship election appeal");
                PredictSample("WHO issues new health guidelines on cholera outbreak prevention in 12 states");
                break;
        }

        return 0;
    }

    private static async Task ScrapeDatasetAsync()
    {
        Console.WriteLine("\n[1/3] Scraping ground-truth pre-categorized news feeds...");
        var articles = new List<NewsArticleRecord>();

        // 1. Ingest clean, curated SeedDataset (210+ samples across all 7 categories)
        Console.WriteLine($" -> Ingesting {SeedDataset.InitialSamples.Count} pristine seed samples...");
        articles.AddRange(SeedDataset.InitialSamples);

        // 2. Fetch live category-specific RSS feeds
        using var handler = new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate };
        using var client = new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");

        foreach (var ep in CategoryFeedConfig.Endpoints)
        {
            try
            {
                Console.WriteLine($" -> Fetching [{ep.Category}] from {ep.Source} ({ep.FeedUrl})...");
                using var response = await client.GetAsync(ep.FeedUrl);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"    ⚠️ Warning: Feed returned status code {response.StatusCode}");
                    continue;
                }

                using var stream = await response.Content.ReadAsStreamAsync();
                using var xmlReader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
                var feed = SyndicationFeed.Load(xmlReader);

                int count = 0;
                foreach (var item in feed.Items)
                {
                    string title = CleanNewsText(item.Title?.Text ?? "");
                    string summary = CleanNewsText(item.Summary?.Text ?? "");

                    if (string.IsNullOrWhiteSpace(title) || title.Length < 8) continue;

                    // Deduplicate against existing articles by title
                    if (articles.Any(a => a.Title.Equals(title, StringComparison.OrdinalIgnoreCase))) continue;

                    articles.Add(new NewsArticleRecord
                    {
                        Title = title,
                        Summary = summary,
                        Category = ep.Category,
                        Source = ep.Source,
                        Url = item.Links.FirstOrDefault()?.Uri.ToString() ?? ""
                    });
                    count++;
                }

                Console.WriteLine($"    Extracted {count} clean articles for '{ep.Category}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ⚠️ Warning: Could not scrape feed {ep.FeedUrl}: {ex.Message}");
            }
        }

        Console.WriteLine($"\nScraped total {articles.Count} articles across {articles.Select(a => a.Category).Distinct().Count()} categories:");
        foreach (var grp in articles.GroupBy(a => a.Category).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"   • {grp.Key,-15}: {grp.Count(),4} samples");
        }

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(DatasetPath, JsonSerializer.Serialize(articles, jsonOptions));
        Console.WriteLine($"Saved dataset to: {DatasetPath}");
    }

    private static void TrainModel()
    {
        Console.WriteLine("\n[2/3] Training ML.NET Multiclass Classification Model...");

        if (!File.Exists(DatasetPath))
        {
            Console.WriteLine($"❌ Error: Dataset file not found at {DatasetPath}. Run 'scrape' first.");
            return;
        }

        var jsonText = File.ReadAllText(DatasetPath);
        var articles = JsonSerializer.Deserialize<List<NewsArticleRecord>>(jsonText) ?? new List<NewsArticleRecord>();

        // Load human correction feedback dataset if present
        string correctedDatasetPath = Path.Combine(AppContext.BaseDirectory, "corrected_dataset.json");
        if (File.Exists(correctedDatasetPath))
        {
            try
            {
                var correctedText = File.ReadAllText(correctedDatasetPath);
                var correctedArticles = JsonSerializer.Deserialize<List<NewsArticleRecord>>(correctedText);
                if (correctedArticles != null && correctedArticles.Any())
                {
                    Console.WriteLine($"💡 Ingesting {correctedArticles.Count} admin category correction feedback samples from {correctedDatasetPath}!");
                    foreach (var corrected in correctedArticles)
                    {
                        corrected.Title = CleanNewsText(corrected.Title);
                        corrected.Summary = CleanNewsText(corrected.Summary);

                        articles.RemoveAll(a => a.Title.Equals(corrected.Title, StringComparison.OrdinalIgnoreCase) ||
                                               (!string.IsNullOrEmpty(a.Url) && a.Url.Equals(corrected.Url, StringComparison.OrdinalIgnoreCase)));

                        // Weighted reinforcement for admin human-verified corrections
                        for (int i = 0; i < 4; i++)
                        {
                            articles.Add(corrected);
                        }
                    }
                    Console.WriteLine($"   Combined dataset count after feedback injection: {articles.Count} samples.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Warning loading correction dataset: {ex.Message}");
            }
        }

        if (!articles.Any())
        {
            Console.WriteLine("❌ Error: Dataset is empty.");
            return;
        }

        var mlContext = new MLContext(seed: 42);

        // Prepare training data samples with 3x title weighting for headline importance
        var trainingDataList = articles.Select(a =>
        {
            var cleanTitle = CleanNewsText(a.Title);
            var cleanSummary = CleanNewsText(a.Summary);
            return new ArticleInput
            {
                Title = cleanTitle,
                Summary = cleanSummary,
                Text = $"{cleanTitle} {cleanTitle} {cleanTitle} {cleanSummary}".Trim(),
                Category = a.Category
            };
        }).ToList();

        IDataView rawDataView = mlContext.Data.LoadFromEnumerable(trainingDataList);

        // Map Category string to numeric Key column ONCE on the training dataset schema
        var labelMapperPipeline = mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: "Label", inputColumnName: nameof(ArticleInput.Category));
        IDataView mappedDataView = labelMapperPipeline.Fit(rawDataView).Transform(rawDataView);

        // Train/Test split (80% train, 20% test) with stratified seed
        var split = mlContext.Data.TrainTestSplit(mappedDataView, testFraction: 0.2, seed: 42);

        // Build advanced feature extraction & classification pipeline
        var textFeaturizerOptions = new TextFeaturizingEstimator.Options
        {
            CaseMode = TextNormalizingEstimator.CaseMode.Lower,
            KeepDiacritics = false,
            KeepPunctuations = false,
            KeepNumbers = false,
            WordFeatureExtractor = new WordBagEstimator.Options
            {
                NgramLength = 2,
                UseAllLengths = true,
                Weighting = NgramExtractingEstimator.WeightingCriteria.TfIdf
            },
            CharFeatureExtractor = null, // Disable char n-grams to remove noisy character substrings
            StopWordsRemoverOptions = new StopWordsRemovingEstimator.Options()
        };

        var trainingPipeline = mlContext.Transforms.Text.FeaturizeText(
                outputColumnName: "Features",
                options: textFeaturizerOptions,
                inputColumnNames: nameof(ArticleInput.Text))
            .Append(mlContext.MulticlassClassification.Trainers.LbfgsMaximumEntropy(
                labelColumnName: "Label",
                featureColumnName: "Features",
                l1Regularization: 0.005f,
                l2Regularization: 0.01f))
            .Append(mlContext.Transforms.Conversion.MapKeyToValue(
                outputColumnName: "PredictedLabel",
                inputColumnName: "PredictedLabel"));

        Console.WriteLine("Fitting L-BFGS Maximum Entropy model on training dataset...");
        var trainedModel = trainingPipeline.Fit(split.TrainSet);

        // Evaluate model
        Console.WriteLine("Evaluating model accuracy on test dataset...");
        var predictions = trainedModel.Transform(split.TestSet);
        var metrics = mlContext.MulticlassClassification.Evaluate(predictions, labelColumnName: "Label");

        Console.WriteLine("\n=================================");
        Console.WriteLine("   Model Evaluation Results");
        Console.WriteLine("=================================");
        Console.WriteLine($" Micro-Accuracy: {metrics.MicroAccuracy:P2}");
        Console.WriteLine($" Macro-Accuracy: {metrics.MacroAccuracy:P2}");
        Console.WriteLine($" Log-Loss:       {metrics.LogLoss:F4}");
        Console.WriteLine($" Log-Loss Reduc: {metrics.LogLossReduction:F4}");
        Console.WriteLine("=================================");

        // Save trained model zip
        mlContext.Model.Save(trainedModel, mappedDataView.Schema, ModelOutputPath);
        Console.WriteLine($"\nSaved trained ML.NET model to: {ModelOutputPath}");

        // Copy model to NewsScraperService directory if it exists
        var serviceModelDirs = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/NewsScraperService/Models")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../src/NewsScraperService/Models")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../src/NewsScraperService/Models"))
        };

        foreach (var dir in serviceModelDirs)
        {
            if (Directory.Exists(dir))
            {
                string targetZip = Path.Combine(dir, "categorizer_model.zip");
                File.Copy(ModelOutputPath, targetZip, overwrite: true);
                Console.WriteLine($"Copied model to NewsScraperService target path: {targetZip}");
                break;
            }
        }
    }

    private static void PredictSample(string text)
    {
        if (!File.Exists(ModelOutputPath))
        {
            Console.WriteLine($"❌ Model file not found at {ModelOutputPath}");
            return;
        }

        var mlContext = new MLContext();
        ITransformer trainedModel = mlContext.Model.Load(ModelOutputPath, out _);
        var predEngine = mlContext.Model.CreatePredictionEngine<ArticleInput, ArticlePrediction>(trainedModel);

        string clean = CleanNewsText(text);
        var sampleInput = new ArticleInput
        {
            Title = clean,
            Summary = string.Empty,
            Text = $"{clean} {clean} {clean}".Trim(),
            Category = string.Empty
        };

        var result = predEngine.Predict(sampleInput);
        float maxScore = result.Score != null && result.Score.Length > 0 ? result.Score.Max() : 0f;
        Console.WriteLine($" Headline: \"{text}\"");
        Console.WriteLine($"  ---> Predicted Category: [{result.PredictedCategory}] (Confidence: {maxScore:P1})\n");
    }

    public static string CleanNewsText(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;

        // 1. Strip HTML tags
        string clean = Regex.Replace(rawText, "<.*?>", " ");

        // 2. Decode HTML entities
        clean = System.Net.WebUtility.HtmlDecode(clean);

        // 3. Strip URL patterns & query params
        clean = Regex.Replace(clean, @"https?://\S+", " ");
        clean = Regex.Replace(clean, @"www\.\S+", " ");
        clean = Regex.Replace(clean, @"\b\w+\.(com|ng|org|net|co|io|africa)\b", " ", RegexOptions.IgnoreCase);

        // 4. Strip common boilerplate markers and metadata
        clean = Regex.Replace(clean, @"Read More:.*", " ", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"The post .* appeared first on .*", " ", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"\(CREDIT:.*?\)", " ", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"\[&#8230;\]", " ", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"\[\.\.\.\]", " ", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"\b(utm_source|utm_medium|utm_campaign|rss|web)\b", " ", RegexOptions.IgnoreCase);

        // 5. Normalize whitespace
        clean = Regex.Replace(clean, @"\s+", " ").Trim();

        return clean;
    }
}
