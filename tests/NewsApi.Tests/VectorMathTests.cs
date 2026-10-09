using NewsApi.Services.Embeddings;
using Xunit;

namespace NewsApi.Tests;

public class VectorMathTests
{
    [Fact]
    public void CosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        float[] a = [1f, 2f, 3f];
        float[] b = [1f, 2f, 3f];

        var sim = VectorMath.CosineSimilarity(a, b);
        Assert.Equal(1.0f, sim, 4);
    }

    [Fact]
    public void CosineSimilarity_OrthogonalVectors_ReturnsZero()
    {
        float[] a = [1f, 0f, 0f];
        float[] b = [0f, 1f, 0f];

        var sim = VectorMath.CosineSimilarity(a, b);
        Assert.Equal(0.0f, sim, 4);
    }

    [Fact]
    public void CosineSimilarity_OppositeVectors_ReturnsNegativeOne()
    {
        float[] a = [1f, 2f, 3f];
        float[] b = [-1f, -2f, -3f];

        var sim = VectorMath.CosineSimilarity(a, b);
        Assert.Equal(-1.0f, sim, 4);
    }

    [Fact]
    public void Normalize_ScalesVectorToUnitLength()
    {
        float[] vec = [3f, 4f, 0f];
        VectorMath.Normalize(vec);

        float sumSquares = 0f;
        foreach (var v in vec) sumSquares += v * v;
        Assert.Equal(1.0f, MathF.Sqrt(sumSquares), 4);
        Assert.Equal(0.6f, vec[0], 4);
        Assert.Equal(0.8f, vec[1], 4);
    }

    [Fact]
    public void CosineSimilarity_EmptyOrMismatched_ReturnsZero()
    {
        float[] a = [1f, 2f];
        float[] b = [1f, 2f, 3f];
        Assert.Equal(0f, VectorMath.CosineSimilarity(a, b));
        Assert.Equal(0f, VectorMath.CosineSimilarity([], []));
    }
}
