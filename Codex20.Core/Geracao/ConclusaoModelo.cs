namespace Codex20.Core.Geracao;

/// <summary>O que o modelo devolveu numa chamada, com o consumo de tokens que ela custou.</summary>
public class ConclusaoModelo
{
    public string Texto { get; init; } = string.Empty;

    public int TokensEntrada { get; init; }

    /// <summary>Inclui os de raciocínio: é assim que a API cobra.</summary>
    public int TokensSaida { get; init; }

    /// <summary>
    /// Parte de <see cref="TokensSaida"/> que o modelo gastou pensando e não aparece no texto.
    /// Zero em modelo sem raciocínio.
    /// </summary>
    public int TokensRaciocinio { get; init; }

    /// <summary>
    /// O modelo parou por bater no limite de tokens de saída. Em modelo de raciocínio o limite pode
    /// acabar antes de ele começar a escrever, e o texto vem vazio.
    /// </summary>
    public bool IsCortada { get; init; }
}
