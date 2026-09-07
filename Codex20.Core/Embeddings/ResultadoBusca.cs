using Codex20.Core.Chunking;

namespace Codex20.Core.Embeddings;

/// <summary>
/// Um chunk devolvido por uma busca vetorial, com o quanto ele casou com a consulta
/// </summary>
public class ResultadoBusca
{
    public Chunk Chunk { get; init; } = new();

    /// <summary>
    /// Similaridade de cosseno: 1 = idêntico, 0 = sem relação
    /// </summary>
    public double Similaridade { get; init; }
}
