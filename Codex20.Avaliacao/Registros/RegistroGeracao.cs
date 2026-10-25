using Codex20.Avaliacao.Gabarito;

namespace Codex20.Avaliacao.Registros;

/// <summary>Uma resposta gerada, com RAG ou sem, e o que é preciso para pontuá-la depois.</summary>
public class RegistroGeracao
{
    public const string ModoComRag = "com-rag";

    public const string ModoSemRag = "sem-rag";

    /// <summary><see cref="ModoComRag"/> ou <see cref="ModoSemRag"/>.</summary>
    public string Modo { get; init; } = string.Empty;

    /// <summary>A partir de 1; passa de 1 só com <c>--repeticoes</c>.</summary>
    public int Rodada { get; init; }

    public string Modelo { get; init; } = string.Empty;

    public string Texto { get; init; } = string.Empty;

    /// <summary>Os trechos que o modelo recebeu, na numeração do prompt. Vazia sem RAG.</summary>
    public List<RegistroTrecho> Trechos { get; init; } = new();

    /// <summary>Números [n] citados que existem no prompt. Vazia sem RAG.</summary>
    public List<int> Citacoes { get; init; } = new();

    /// <summary>Os chunks das <see cref="Citacoes"/>, na mesma ordem: a base da P/R/F1 por chunk.</summary>
    public List<Guid> ChunksCitados { get; init; } = new();

    public List<int> CitacoesInexistentes { get; init; } = new();

    /// <summary>
    /// Livro e página citados — a base da P/R/F1 por página, que é a que compara com × sem RAG. Com
    /// RAG vêm das páginas dos chunks citados; sem RAG, do "(Livro do Jogador, p. 95)" no texto, por
    /// regex: confira à mão, o modelo nem sempre segue o formato.
    /// </summary>
    public List<IntervaloPaginas> PaginasCitadas { get; init; } = new();

    /// <summary>
    /// Livros citados, com ou sem página. Sem RAG o modelo costuma citar só o livro ("(Livro do
    /// Jogador)"), e sem isto a resposta ficaria sem fonte nenhuma registrada.
    /// </summary>
    public List<string> LivrosCitados { get; init; } = new();

    public int TokensEntrada { get; init; }

    public int TokensSaida { get; init; }

    public int TokensRaciocinio { get; init; }

    /// <summary>Zero sem RAG.</summary>
    public double DuracaoBuscaMs { get; init; }

    public double DuracaoGeracaoMs { get; init; }

    public bool IsCortada { get; init; }

    public string? Observacao { get; init; }
}
