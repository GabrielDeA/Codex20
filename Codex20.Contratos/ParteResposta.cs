using System.Text.Json.Serialization;

namespace Codex20.Contratos;

/// <summary>
/// Um pedaço da resposta: ou texto corrido, ou uma citação a uma fonte. "[1][3]" chega como duas
/// partes, uma por fonte, para o App não precisar interpretar colchetes.
/// </summary>
public class ParteResposta
{
    /// <summary>Vazio quando a parte é uma citação.</summary>
    public string Texto { get; set; } = string.Empty;

    /// <summary>Número da fonte em <see cref="RespostaConsulta.Fontes"/>; nulo no texto corrido.</summary>
    public int? NumeroFonte { get; set; }

    /// <summary>Calculado dos dois lados, não precisa viajar no JSON.</summary>
    [JsonIgnore]
    public bool IsCitacao => NumeroFonte is not null;
}
