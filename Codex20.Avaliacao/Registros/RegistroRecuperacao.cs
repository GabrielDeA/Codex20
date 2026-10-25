namespace Codex20.Avaliacao.Registros;

/// <summary>
/// Como uma estratégia de busca se saiu numa pergunta. É daqui que saem Hit@k e MRR: basta a
/// posição do primeiro chunk de referência.
/// </summary>
public class RegistroRecuperacao
{
    /// <summary>cosseno, bm25 ou rrf.</summary>
    public string Estrategia { get; init; } = string.Empty;

    /// <summary>Quantos resultados foram pedidos; referência abaixo disso fica com posição nula.</summary>
    public int Profundidade { get; init; }

    /// <summary>
    /// Melhor posição entre os chunks de referência (a partir de 1); <c>null</c> se nenhum apareceu
    /// ou se a pergunta não tem resposta nos livros.
    /// </summary>
    public int? PosicaoPrimeiraReferencia { get; init; }

    public List<PosicaoReferencia> PosicoesReferencia { get; init; } = new();

    /// <summary>Os primeiros do ranking, tantos quantos vão para o prompt na geração.</summary>
    public List<RegistroTrecho> Top { get; init; } = new();

    public double DuracaoMs { get; init; }

    public string? Observacao { get; init; }
}
