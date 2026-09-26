namespace Codex20.Core.Geracao;

/// <summary>
/// Um pedaço do texto da resposta: ou texto corrido, ou uma citação como "[1][3]", já com os
/// números separados para a tela virar link.
/// </summary>
public class SegmentoResposta
{
    public string Texto { get; init; } = string.Empty;

    /// <summary>Vazia quando o segmento é texto corrido.</summary>
    public List<int> Numeros { get; init; } = new();

    public bool IsCitacao => Numeros.Count > 0;
}
