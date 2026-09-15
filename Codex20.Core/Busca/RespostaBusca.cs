using Codex20.Core.Embeddings;

namespace Codex20.Core.Busca;

/// <summary>O ranking que uma estratégia devolveu para uma consulta.</summary>
public class RespostaBusca
{
    public string NomeEstrategia { get; init; } = string.Empty;

    public string RotuloPontuacao { get; init; } = string.Empty;

    public List<ResultadoBusca> Resultados { get; init; } = new();

    /// <summary>
    /// Cronometrado por quem chamou a estratégia, não por ela mesma: assim todas são medidas do
    /// mesmo jeito.
    /// </summary>
    public TimeSpan Duracao { get; set; }

    /// <summary>
    /// Recado para a tela: consulta sem termo pesquisável, estratégia indisponível, erro. Uma
    /// estratégia que falha na comparação vira uma coluna explicando-se, não uma exceção.
    /// </summary>
    public string? Observacao { get; set; }
}
