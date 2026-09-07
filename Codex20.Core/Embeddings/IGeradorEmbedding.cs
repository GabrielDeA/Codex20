namespace Codex20.Core.Embeddings;

/// <summary>Gera vetores de embedding para textos de chunk.</summary>
public interface IGeradorEmbedding
{
    /// <summary>
    /// Modelo usado (fica gravado junto do vetor, para saber o que reprocessar)
    /// </summary>
    string Modelo { get; }

    /// <summary>
    /// Dimensão dos vetores gerados
    /// </summary>
    int Dimensoes { get; }

    /// <summary>
    /// Gera um vetor por texto, na mesma ordem da lista recebida
    /// </summary>
    Task<List<float[]>> GerarEmLoteAsync(List<string> textos);
}
