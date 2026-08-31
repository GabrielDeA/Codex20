namespace Codex20.Core.Chunking;

/// <summary>Um pedaço de texto pronto para embedding/indexação no RAG.</summary>
public class Chunk
{
    /// <summary>Conteúdo do chunk (texto + tabelas HTML inteiras quando houver).</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Nome da entidade do jogo (criatura, magia, item mágico) quando o chunk corresponde a
    /// uma entidade reconhecida; <c>null</c> para chunks de fallback (prosa genérica).
    /// </summary>
    public string? EntityName { get; init; }

    /// <summary>Primeira página de origem, quando conhecida.</summary>
    public int? PageStart { get; init; }

    /// <summary>Última página de origem, quando conhecida.</summary>
    public int? PageEnd { get; init; }

    /// <summary>Identificador do livro de origem.</summary>
    public string Book { get; init; } = string.Empty;

    /// <summary>Estratégia que produziu o chunk (para diagnóstico).</summary>
    public string StrategyName { get; init; } = string.Empty;

    /// <summary>
    /// <c>true</c> quando o chunk veio do <see cref="ParagraphTokenChunkingStrategy"/> por não
    /// haver entidade reconhecida ou por a entidade estourar o orçamento de tokens.
    /// </summary>
    public bool IsFallback { get; init; }
}
