using NewsApi.Services.Embeddings;
using Xunit;

namespace NewsApi.Tests;

public class EmbeddingProviderTests
{
    private readonly LocalFallbackEmbeddingProvider _provider = new();

    [Fact]
    public async Task GenerateEmbedding_Returns768Dimensions()
    {
        var vector = await _provider.GenerateEmbeddingAsync("President Bola Tinubu meets with security chiefs at Aso Rock Villa");

        Assert.NotNull(vector);
        Assert.Equal(768, vector.Length);

        // Verify it is unit normalized
        float sumSquares = 0f;
        foreach (var v in vector) sumSquares += v * v;
        Assert.InRange(MathF.Sqrt(sumSquares), 0.999f, 1.001f);
    }

    [Fact]
    public async Task GenerateEmbedding_SimilarArticles_HaveHighCosineSimilarity()
    {
        var textA = "CBN increases Monetary Policy Rate to 27.25% to combat inflation and stabilise Naira";
        var textB = "Central Bank of Nigeria raises MPR interest benchmark rate to 27.25 percent to control inflation";
        var textC = "Super Eagles coach names 23-man squad for upcoming AFCON qualifying match in Uyo";

        var vecA = await _provider.GenerateEmbeddingAsync(textA);
        var vecB = await _provider.GenerateEmbeddingAsync(textB);
        var vecC = await _provider.GenerateEmbeddingAsync(textC);

        var simAB = VectorMath.CosineSimilarity(vecA, vecB);
        var simAC = VectorMath.CosineSimilarity(vecA, vecC);

        // Similar articles should have significantly higher similarity than unrelated sports article
        Assert.True(simAB > 0.65f, $"Expected simAB > 0.65, got {simAB}");
        Assert.True(simAC < 0.40f, $"Expected simAC < 0.40, got {simAC}");
        Assert.True(simAB > simAC + 0.30f);
    }

    [Fact]
    public async Task GenerateEmbeddingsBatch_ProducesConsistentResults()
    {
        var texts = new[]
        {
            "Dangote refinery begins commercial supply of petrol to Nigerian domestic market",
            "NNPCL adjustments in petrol pump prices across Lagos and Abuja fuel stations"
        };

        var batch = await _provider.GenerateEmbeddingsBatchAsync(texts);
        var single0 = await _provider.GenerateEmbeddingAsync(texts[0]);

        Assert.Equal(2, batch.Count);
        Assert.Equal(768, batch[0].Length);
        Assert.Equal(768, batch[1].Length);

        // Batch result should match single generation exactly
        var sim = VectorMath.CosineSimilarity(batch[0], single0);
        Assert.Equal(1.0f, sim, 4);
    }
}
