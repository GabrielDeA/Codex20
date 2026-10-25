using Codex20.Avaliacao.Gabarito;

namespace Codex20.Avaliacao.Registros;

/// <summary>Uma linha do JSONL: a pergunta, o gabarito dela e tudo o que o sistema fez com ela.</summary>
public class RegistroPergunta
{
    public string Id { get; init; } = string.Empty;

    public string Pergunta { get; init; } = string.Empty;

    public string Tipo { get; init; } = string.Empty;

    public string Livro { get; init; } = string.Empty;

    public bool IsRespondivel { get; init; }

    public List<Guid> ChunksReferencia { get; init; } = new();

    public List<IntervaloPaginas> PaginasReferencia { get; init; } = new();

    /// <summary>Uma por estratégia: cosseno, bm25, rrf.</summary>
    public List<RegistroRecuperacao> Recuperacao { get; init; } = new();

    /// <summary>Com RAG e sem RAG, uma de cada por rodada.</summary>
    public List<RegistroGeracao> Geracoes { get; init; } = new();
}
