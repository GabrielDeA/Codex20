namespace Codex20.Core.Busca;

/// <summary>Como uma perna de uma fusão ranqueou um chunk — é o que explica a colocação final.</summary>
public class ContribuicaoEstrategia
{
    public string NomeEstrategia { get; init; } = string.Empty;

    public int Posicao { get; init; }

    public double Pontuacao { get; init; }
}
