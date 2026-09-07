using System.Security.Cryptography;
using System.Text;

namespace Codex20.Core.Chunking;

/// <summary>Um pedaço de texto pronto para embedding/indexação no RAG.</summary>
public class Chunk
{
    /// <summary>
    /// SHA-256 de <see cref="Livro"/> + <see cref="Texto"/>. Serve de ponte entre o JSON de chunks
    /// e o vector store: como o mesmo conteúdo sempre gera o mesmo Id, um resultado de busca pode
    /// ser localizado no JSON por ele. Note que texto repetido dentro do livro cai no mesmo Id, e
    /// a gravação depende disso para não violar a chave primária.
    /// </summary>
    public Guid Id => GerarId(Livro, Texto);

    /// <summary>Conteúdo do chunk (texto + tabelas HTML inteiras quando houver).</summary>
    public string Texto { get; init; } = string.Empty;

    /// <summary>
    /// Nome da entidade do jogo (criatura, magia, item mágico) quando o chunk corresponde a
    /// uma entidade reconhecida; <c>null</c> para chunks de fallback (prosa genérica).
    /// </summary>
    public string? NomeEntidade { get; init; }

    /// <summary>Primeira página de origem, quando conhecida.</summary>
    public int? PaginaInicio { get; init; }

    /// <summary>Última página de origem, quando conhecida.</summary>
    public int? PaginaFim { get; init; }

    /// <summary>Identificador do livro de origem.</summary>
    public string Livro { get; init; } = string.Empty;

    /// <summary>Strategy que produziu o chunk (para diagnóstico).</summary>
    public string NomeStrategy { get; init; } = string.Empty;

    /// <summary>
    /// <c>true</c> quando o chunk veio da <see cref="ChunkingStrategyParagrafoToken"/> por não
    /// haver entidade reconhecida ou por a entidade estourar o orçamento de tokens.
    /// </summary>
    public bool IsFallback { get; init; }

    private static Guid GerarId(string livro, string texto)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(livro + "\n" + texto));
        return new Guid(hash[..16]);
    }
}
