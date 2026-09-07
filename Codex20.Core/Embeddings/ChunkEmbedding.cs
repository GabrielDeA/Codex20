using Codex20.Core.Chunking;

namespace Codex20.Core.Embeddings;

/// <summary>Vetor gerado para um <see cref="Chunk"/>.</summary>
public class ChunkEmbedding
{
    public Guid ChunkId { get; init; }

    public float[] Vetor { get; init; } = Array.Empty<float>();

    public string Modelo { get; init; } = string.Empty;
}
