using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NewsApi.Services;

public record CategorizerTrainResult(
    bool Success,
    double MicroAccuracy,
    double MacroAccuracy,
    double LogLoss,
    double LogLossReduction,
    int TotalTrainingSamples,
    int HumanCorrectionsUsed,
    DateTime TrainedAt,
    string Message
);

public record CategorizerStatusDto(
    bool IsOperational,
    bool IsTraining,
    DateTime? LastTrainedAt,
    double MicroAccuracy,
    double MacroAccuracy,
    double LogLoss,
    double LogLossReduction,
    int TotalTrainingSamples,
    int TotalCorrections,
    int CorrectionsSinceLastTraining,
    int AutoRetrainThreshold,
    string ModelPath
);

public record CategorizerPredictionDto(
    string PredictedCategory,
    float Confidence,
    Dictionary<string, float> Scores
);

public record CategorizerCorrectionDto(
    string Id,
    string ArticleId,
    string Title,
    string? Summary,
    string OldCategory,
    string NewCategory,
    string? Source,
    string? Url,
    DateTime CreatedAt
);

public interface ICategorizerTrainingService
{
    bool IsTraining { get; }
    Task<CategorizerTrainResult> TrainAsync(CancellationToken ct = default);
    CategorizerStatusDto GetStatus();
    CategorizerPredictionDto Predict(string title, string? summary = null);
    byte[]? GetModelBytes();
    void CheckAndTriggerAutoRetrain();
}
