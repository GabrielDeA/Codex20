namespace Codex20.Avaliacao.Registros;

/// <summary>
/// Gravado ao lado do JSONL (<c>.meta.json</c>): com que configuração a avaliação rodou. Os prompts
/// vão inteiros porque são parte do protocolo que o trabalho descreve.
/// </summary>
public class MetadadosAvaliacao
{
    public DateTime DataHora { get; init; }

    public string Modelo { get; init; } = string.Empty;

    public string ModeloEmbedding { get; init; } = string.Empty;

    /// <summary>A estratégia que alimenta a geração com RAG (a mesma da Api).</summary>
    public string EstrategiaGeracao { get; init; } = string.Empty;

    public int QuantidadeTrechos { get; init; }

    public int Profundidade { get; init; }

    public int Repeticoes { get; init; }

    public string ArquivoPerguntas { get; init; } = string.Empty;

    public int QuantidadePerguntas { get; init; }

    public Dictionary<string, int> ChunksPorLivro { get; init; } = new();

    public string InstrucoesComRag { get; init; } = string.Empty;

    public string InstrucoesSemRag { get; init; } = string.Empty;
}
