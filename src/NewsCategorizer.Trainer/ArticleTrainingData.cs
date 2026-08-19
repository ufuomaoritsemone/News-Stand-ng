using Microsoft.ML.Data;

namespace NewsCategorizer.Trainer;

public class ArticleInput
{
    [LoadColumn(0)]
    public string Title { get; set; } = string.Empty;

    [LoadColumn(1)]
    public string Summary { get; set; } = string.Empty;

    [LoadColumn(2)]
    public string Text { get; set; } = string.Empty;

    [LoadColumn(3)]
    public string Category { get; set; } = string.Empty;
}

public class ArticlePrediction
{
    [ColumnName("PredictedLabel")]
    public string PredictedCategory { get; set; } = string.Empty;

    public float[] Score { get; set; } = Array.Empty<float>();
}

public class NewsArticleRecord
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
