using Codex20.Core.Chunking;
using Codex20.Core.Embeddings;

namespace Codex20.Core.Busca;

/// <summary>Índice de texto para busca léxica (palavra-chave), separado do índice vetorial.</summary>
public interface IIndiceTextual
{
    /// <summary>Cria o índice se ainda não existir.</summary>
    Task PrepararAsync();

    /// <summary>
    /// Indexa os chunks, substituindo tudo o que já havia dos livros envolvidos. Devolve quantas
    /// linhas foram gravadas.
    /// </summary>
    Task<int> IndexarAsync(List<Chunk> chunks);

    /// <summary>
    /// Os melhores chunks para a consulta, já ordenados e com pontuação maior = melhor. A consulta
    /// é texto livre do usuário: sanitizá-la é responsabilidade do índice.
    /// </summary>
    Task<List<ResultadoBusca>> BuscarTextoAsync(string consulta, int quantidade);

    /// <summary>Quantos chunks cada livro tem no índice.</summary>
    Task<Dictionary<string, int>> ContarPorLivroAsync();
}
