using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Text;
using NewsApi.Data;
using NewsCategorizer.Trainer;

namespace NewsApi.Services;

public class CategorizerTrainingService : ICategorizerTrainingService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CategorizerTrainingService> _logger;
    private readonly SemaphoreSlim _trainLock = new(1, 1);
    private readonly object _engineLock = new();

    private PredictionEngine<ArticleInput, ArticlePrediction>? _predictionEngine;
    private ITransformer? _trainedModel;
    private DataViewSchema? _modelSchema;

    private bool _isTraining;
    private DateTime? _lastTrainedAt;
    private double _microAccuracy = 0.7283;
    private double _macroAccuracy = 0.5728;
    private double _logLoss = 0.8250;
    private double _logLossReduction = 0.5128;
    private int _totalTrainingSamples;
    private int _correctionsAtLastTraining;
    private const int AutoRetrainThreshold = 20;

    private readonly string _primaryModelPath;

    public bool IsTraining => _isTraining;

    public CategorizerTrainingService(
        IServiceScopeFactory scopeFactory,
        ILogger<CategorizerTrainingService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;

        string baseDir = AppContext.BaseDirectory;
        string modelsDir = Path.Combine(baseDir, "Models");
        Directory.CreateDirectory(modelsDir);
        _primaryModelPath = Path.Combine(modelsDir, "categorizer_model.zip");

        // Attempt initial load from existing model file if present
        TryLoadExistingModel();
    }

    private void TryLoadExistingModel()
    {
        try
        {
            var searchPaths = new[]
            {
                _primaryModelPath,
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../src/NewsScraperService/Models/categorizer_model.zip")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/NewsScraperService/Models/categorizer_model.zip")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../src/NewsCategorizer.Trainer/categorizer_model.zip"))
            };

            string? found = searchPaths.FirstOrDefault(File.Exists);
            if (found != null)
            {
                var mlContext = new MLContext(seed: 42);
                lock (_engineLock)
                {
                    _trainedModel = mlContext.Model.Load(found, out var schema);
                    _modelSchema = schema;
                    _predictionEngine = mlContext.Model.CreatePredictionEngine<ArticleInput, ArticlePrediction>(_trainedModel);
                    _lastTrainedAt = File.GetLastWriteTimeUtc(found);
                }
                _logger.LogInformation("Loaded active ML Categorizer model from {Path}", found);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not pre-load categorizer model on startup.");
        }
    }

    public CategorizerStatusDto GetStatus()
    {
        int totalCorrections = 0;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NewsDbContext>();
            totalCorrections = db.CategoryCorrections.Count();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to count CategoryCorrections for status.");
        }

        int correctionsSince = Math.Max(0, totalCorrections - _correctionsAtLastTraining);
        bool isOperational = File.Exists(_primaryModelPath) || _predictionEngine != null;

        return new CategorizerStatusDto(
            IsOperational: isOperational,
            IsTraining: _isTraining,
            LastTrainedAt: _lastTrainedAt,
            MicroAccuracy: _microAccuracy,
            MacroAccuracy: _macroAccuracy,
            LogLoss: _logLoss,
            LogLossReduction: _logLossReduction,
            TotalTrainingSamples: _totalTrainingSamples,
            TotalCorrections: totalCorrections,
            CorrectionsSinceLastTraining: correctionsSince,
            AutoRetrainThreshold: AutoRetrainThreshold,
            ModelPath: _primaryModelPath
        );
    }

    public CategorizerPredictionDto Predict(string title, string? summary = null)
    {
        string cleanTitle = CleanNewsText(title);
        string cleanSummary = CleanNewsText(summary ?? string.Empty);

        if (_predictionEngine == null)
        {
            // Fallback rule if no ML model loaded yet
            return new CategorizerPredictionDto(
                PredictedCategory: "General",
                Confidence: 0.50f,
                Scores: new Dictionary<string, float> { { "General", 0.50f } }
            );
        }

        lock (_engineLock)
        {
            var input = new ArticleInput
            {
                Title = cleanTitle,
                Summary = cleanSummary,
                Text = $"{cleanTitle} {cleanTitle} {cleanTitle} {cleanSummary}".Trim(),
                Category = string.Empty
            };

            var pred = _predictionEngine.Predict(input);
            float confidence = pred.Score != null && pred.Score.Length > 0 ? pred.Score.Max() : 0.5f;

            var scoreMap = new Dictionary<string, float>();
            if (_modelSchema != null && pred.Score != null)
            {
                try
                {
                    var scoreCol = _modelSchema.GetColumnOrNull("Score");
                    if (scoreCol.HasValue && scoreCol.Value.Annotations.Schema.GetColumnOrNull("SlotNames").HasValue)
                    {
                        var slotNames = new VBuffer<ReadOnlyMemory<char>>();
                        scoreCol.Value.GetSlotNames(ref slotNames);
                        var names = slotNames.DenseValues().Select(v => v.ToString()).ToArray();
                        for (int i = 0; i < Math.Min(names.Length, pred.Score.Length); i++)
                        {
                            scoreMap[names[i]] = pred.Score[i];
                        }
                    }
                }
                catch
                {
                    // Fallback to predicted category below
                }
            }

            if (scoreMap.Count == 0 && !string.IsNullOrWhiteSpace(pred.PredictedCategory))
            {
                scoreMap[pred.PredictedCategory] = confidence;
            }

            return new CategorizerPredictionDto(
                PredictedCategory: string.IsNullOrWhiteSpace(pred.PredictedCategory) ? "General" : pred.PredictedCategory,
                Confidence: confidence,
                Scores: scoreMap
            );
        }
    }

    public byte[]? GetModelBytes()
    {
        if (File.Exists(_primaryModelPath))
        {
            return File.ReadAllBytes(_primaryModelPath);
        }
        return null;
    }

    public void CheckAndTriggerAutoRetrain()
    {
        if (_isTraining) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NewsDbContext>();
            var count = db.CategoryCorrections.Count();

            // Trigger automatically every AutoRetrainThreshold corrections
            if (count > 0 && count % AutoRetrainThreshold == 0 && count != _correctionsAtLastTraining)
            {
                _logger.LogInformation("Auto-retrain threshold ({Threshold}) reached with {Count} total corrections. Initiating background re-training...",
                    AutoRetrainThreshold, count);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await TrainAsync(CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Background auto-retraining encountered an error.");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking auto-retrain trigger.");
        }
    }

    public async Task<CategorizerTrainResult> TrainAsync(CancellationToken ct = default)
    {
        if (!await _trainLock.WaitAsync(0, ct))
        {
            return new CategorizerTrainResult(
                Success: false,
                MicroAccuracy: _microAccuracy,
                MacroAccuracy: _macroAccuracy,
                LogLoss: _logLoss,
                LogLossReduction: _logLossReduction,
                TotalTrainingSamples: _totalTrainingSamples,
                HumanCorrectionsUsed: 0,
                TrainedAt: _lastTrainedAt ?? DateTime.UtcNow,
                Message: "A re-training job is already currently running."
            );
        }

        _isTraining = true;
        try
        {
            _logger.LogInformation("Starting ML.NET Categorizer closed-loop re-training...");

            // 1. Gather SeedDataset samples
            var articles = new List<NewsArticleRecord>(SeedDataset.InitialSamples);
            int humanCorrectionsCount = 0;

            // 2. Ingest manual human corrections from the database with 5x reinforcement
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<NewsDbContext>();
                var corrections = await db.CategoryCorrections
                    .AsNoTracking()
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync(ct);

                humanCorrectionsCount = corrections.Count;
                _correctionsAtLastTraining = humanCorrectionsCount;

                foreach (var c in corrections)
                {
                    var cleanTitle = CleanNewsText(c.Title);
                    var cleanSummary = CleanNewsText(c.Summary ?? string.Empty);

                    // Purge conflicting historical labels for this headline
                    articles.RemoveAll(a =>
                        a.Title.Equals(cleanTitle, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(a.Url) && !string.IsNullOrEmpty(c.Url) && a.Url.Equals(c.Url, StringComparison.OrdinalIgnoreCase)));

                    // 5x weighted reinforcement for human-verified label
                    var record = new NewsArticleRecord
                    {
                        Title    = cleanTitle,
                        Summary  = cleanSummary,
                        Category = c.NewCategory,
                        Source   = c.Source ?? "HumanEditor",
                        Url      = c.Url ?? string.Empty
                    };

                    for (int i = 0; i < 5; i++)
                    {
                        articles.Add(record);
                    }
                }
            }

            _totalTrainingSamples = articles.Count;
            _logger.LogInformation("Assembled dataset with {Total} samples ({Corrections} human corrections with 5x reinforcement).",
                _totalTrainingSamples, humanCorrectionsCount);

            var mlContext = new MLContext(seed: 42);

            // 3. Prepare training data inputs with 3x title duplication to emphasize headline tokens
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

            // Map Category string to numeric Key column
            var labelMapperPipeline = mlContext.Transforms.Conversion.MapValueToKey(
                outputColumnName: "Label",
                inputColumnName: nameof(ArticleInput.Category));
            IDataView mappedDataView = labelMapperPipeline.Fit(rawDataView).Transform(rawDataView);

            // 80/20 train/test split
            var split = mlContext.Data.TrainTestSplit(mappedDataView, testFraction: 0.2, seed: 42);

            // Text featurization
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
                CharFeatureExtractor = null,
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

            _logger.LogInformation("Fitting L-BFGS Maximum Entropy multi-class model...");
            var trainedModel = trainingPipeline.Fit(split.TrainSet);

            _logger.LogInformation("Evaluating model on test split...");
            var predictions = trainedModel.Transform(split.TestSet);
            var metrics = mlContext.MulticlassClassification.Evaluate(predictions, labelColumnName: "Label");

            _microAccuracy = metrics.MicroAccuracy;
            _macroAccuracy = metrics.MacroAccuracy;
            _logLoss = metrics.LogLoss;
            _logLossReduction = metrics.LogLossReduction;
            _lastTrainedAt = DateTime.UtcNow;

            _logger.LogInformation("Training Evaluation: Micro-Accuracy={Micro:P2}, Macro-Accuracy={Macro:P2}, Log-Loss={Loss:F4}",
                _microAccuracy, _macroAccuracy, _logLoss);

            // 4. Save trained model locally
            mlContext.Model.Save(trainedModel, mappedDataView.Schema, _primaryModelPath);
            _logger.LogInformation("Saved categorizer model to primary path: {Path}", _primaryModelPath);

            // 5. Propagate model to NewsScraperService and NewsCategorizer.Trainer target paths for hot-reload
            var targetDirs = new[]
            {
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../src/NewsScraperService/Models")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/NewsScraperService/Models")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../src/NewsCategorizer.Trainer")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/NewsCategorizer.Trainer"))
            };

            foreach (var dir in targetDirs)
            {
                try
                {
                    if (Directory.Exists(dir))
                    {
                        string destZip = Path.Combine(dir, "categorizer_model.zip");
                        File.Copy(_primaryModelPath, destZip, overwrite: true);
                        _logger.LogInformation("Propagated updated model to: {Target}", destZip);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to copy model to target dir {Dir}", dir);
                }
            }

            // 6. Update in-memory prediction engine
            lock (_engineLock)
            {
                _trainedModel = trainedModel;
                _modelSchema = mappedDataView.Schema;
                _predictionEngine = mlContext.Model.CreatePredictionEngine<ArticleInput, ArticlePrediction>(trainedModel);
            }

            return new CategorizerTrainResult(
                Success: true,
                MicroAccuracy: _microAccuracy,
                MacroAccuracy: _macroAccuracy,
                LogLoss: _logLoss,
                LogLossReduction: _logLossReduction,
                TotalTrainingSamples: _totalTrainingSamples,
                HumanCorrectionsUsed: humanCorrectionsCount,
                TrainedAt: _lastTrainedAt.Value,
                Message: $"Successfully re-trained on {_totalTrainingSamples} samples ({humanCorrectionsCount} human corrections). Micro-Accuracy: {_microAccuracy:P1}, Macro-Accuracy: {_macroAccuracy:P1}."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute ML.NET Categorizer re-training.");
            return new CategorizerTrainResult(
                Success: false,
                MicroAccuracy: _microAccuracy,
                MacroAccuracy: _macroAccuracy,
                LogLoss: _logLoss,
                LogLossReduction: _logLossReduction,
                TotalTrainingSamples: _totalTrainingSamples,
                HumanCorrectionsUsed: 0,
                TrainedAt: _lastTrainedAt ?? DateTime.UtcNow,
                Message: $"Re-training failed: {ex.Message}"
            );
        }
        finally
        {
            _isTraining = false;
            _trainLock.Release();
        }
    }

    public static string CleanNewsText(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;

        string clean = Regex.Replace(rawText, "<.*?>", " ");
        clean = WebUtility.HtmlDecode(clean);
        clean = Regex.Replace(clean, @"https?://\S+", " ");
        clean = Regex.Replace(clean, @"www\.\S+", " ");
        clean = Regex.Replace(clean, @"\b\w+\.(com|ng|org|net|co|io|africa)\b", " ", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"Read More:.*", " ", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"The post .* appeared first on .*", " ", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"\(CREDIT:.*?\)", " ", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"\[&#8230;\]", " ", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"\[\.\.\.\]", " ", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"\b(utm_source|utm_medium|utm_campaign|rss|web)\b", " ", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"\s+", " ").Trim();

        return clean;
    }
}
