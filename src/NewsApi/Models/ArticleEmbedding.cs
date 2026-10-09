using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewsApi.Models;

public class ArticleEmbedding
{
    [Key]
    [MaxLength(64)]
    public string ArticleId { get; set; } = string.Empty;
    public Article? Article { get; set; }

    public byte[] VectorBlob { get; set; } = [];
    public int Dimensions { get; set; } = 768;

    [MaxLength(100)]
    public string Model { get; set; } = "gemini-embedding-001";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public float[] Vector
    {
        get
        {
            if (VectorBlob == null || VectorBlob.Length == 0) return [];
            var floats = new float[VectorBlob.Length / sizeof(float)];
            Buffer.BlockCopy(VectorBlob, 0, floats, 0, VectorBlob.Length);
            return floats;
        }
        set
        {
            if (value == null || value.Length == 0)
            {
                VectorBlob = [];
                return;
            }
            var bytes = new byte[value.Length * sizeof(float)];
            Buffer.BlockCopy(value, 0, bytes, 0, bytes.Length);
            VectorBlob = bytes;
            Dimensions = value.Length;
        }
    }
}
