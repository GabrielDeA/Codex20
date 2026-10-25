using Codex20.Core.Embeddings;

namespace Codex20.Avaliacao.Registros;

/// <summary>
/// Um chunk recuperado, sem o texto: o texto está no <c>*.chunks.json</c> pelo Id, e repeti-lo em
/// cada linha só incharia o JSONL.
/// </summary>
public class RegistroTrecho
{
    /// <summary>Posição no ranking, que também é o número [n] do trecho no prompt.</summary>
    public int Numero { get; init; }

    public Guid ChunkId { get; init; }

    public string Livro { get; init; } = string.Empty;

    public int? PaginaInicio { get; init; }

    public int? PaginaFim { get; init; }

    public string? NomeEntidade { get; init; }

    public double Pontuacao { get; init; }

    /// <summary>O chunk é um dos de referência da pergunta.</summary>
    public bool IsReferencia { get; init; }

    public static RegistroTrecho Criar(ResultadoBusca resultado, int numero, List<Guid> chunksReferencia)
    {
        return new RegistroTrecho
        {
            Numero = numero,
            ChunkId = resultado.Chunk.Id,
            Livro = resultado.Chunk.Livro,
            PaginaInicio = resultado.Chunk.PaginaInicio,
            PaginaFim = resultado.Chunk.PaginaFim,
            NomeEntidade = resultado.Chunk.NomeEntidade,
            Pontuacao = Math.Round(resultado.Pontuacao, 6),
            IsReferencia = chunksReferencia.Contains(resultado.Chunk.Id),
        };
    }
}
