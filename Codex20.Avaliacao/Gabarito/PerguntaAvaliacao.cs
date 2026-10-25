namespace Codex20.Avaliacao.Gabarito;

/// <summary>Uma linha do CSV de perguntas: a pergunta e o gabarito dela, escrito a partir dos livros.</summary>
public class PerguntaAvaliacao
{
    public string Id { get; init; } = string.Empty;

    public string Pergunta { get; init; } = string.Empty;

    /// <summary>nominal, parafrase ou combinacao (sem acento, já normalizado).</summary>
    public string Tipo { get; init; } = string.Empty;

    /// <summary>Livro de onde a resposta sai; texto livre (ex. "jogador", "jogador+mestre").</summary>
    public string Livro { get; init; } = string.Empty;

    public string RespostaReferencia { get; init; } = string.Empty;

    public List<IntervaloPaginas> PaginasReferencia { get; init; } = new();

    /// <summary>
    /// Os <c>Chunk.Id</c> que trazem a resposta. Qualquer um deles entre os recuperados conta como
    /// acerto da busca; vazia nas perguntas sem resposta nos livros.
    /// </summary>
    public List<Guid> ChunksReferencia { get; init; } = new();

    /// <summary><c>false</c> nas perguntas sem resposta nos livros, que testam a recusa (RF04).</summary>
    public bool IsRespondivel { get; init; }

    /// <summary>Linha no CSV, para as mensagens de erro apontarem onde corrigir.</summary>
    public int Linha { get; init; }
}
