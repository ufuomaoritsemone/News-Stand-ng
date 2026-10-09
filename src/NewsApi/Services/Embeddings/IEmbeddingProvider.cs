namespace NewsApi.Services.Embeddings;

public interface IEmbeddingProvider
{
    int Dimensions { get; }
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    Task<List<float[]>> GenerateEmbeddingsBatchAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);
}
