namespace NewsApi.Services.Embeddings;

public static class VectorMath
{
    public static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length || a.IsEmpty) return 0f;

        float dot = 0f;
        float normA = 0f;
        float normB = 0f;

        for (int i = 0; i < a.Length; i++)
        {
            float va = a[i];
            float vb = b[i];
            dot += va * vb;
            normA += va * va;
            normB += vb * vb;
        }

        if (normA <= 0f || normB <= 0f) return 0f;
        return dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }

    public static void Normalize(Span<float> vector)
    {
        float norm = 0f;
        for (int i = 0; i < vector.Length; i++)
        {
            norm += vector[i] * vector[i];
        }
        if (norm <= 0f) return;
        float invNorm = 1.0f / MathF.Sqrt(norm);
        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] *= invNorm;
        }
    }
}
