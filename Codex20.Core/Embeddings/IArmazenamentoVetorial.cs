using Codex20.Core.Chunking;

namespace Codex20.Core.Embeddings;

/// <summary>Vector store: guarda chunk + vetor e busca por similaridade.</summary>
public interface IArmazenamentoVetorial
{
    /// <summary>Cria a coleção se ainda não existir.</summary>
    Task PrepararAsync();

    /// <summary>
    /// Grava os chunks com seus vetores, substituindo tudo o que já havia dos livros envolvidos.
    /// Cada <paramref name="embeddings"/> é casado com o chunk de mesmo <see cref="Chunk.Id"/>.
    /// Devolve quantas linhas foram gravadas.
    /// </summary>
    Task<int> SalvarAsync(List<Chunk> chunks, List<ChunkEmbedding> embeddings);

    Task<List<ResultadoBusca>> BuscarSimilaresAsync(float[] vetorConsulta, int quantidade);

    /// <summary>Quantos vetores cada livro tem no banco.</summary>
    Task<Dictionary<string, int>> ContarPorLivroAsync();
}
