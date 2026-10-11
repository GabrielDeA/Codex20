namespace Codex20.Contratos;

/// <summary>De onde saiu uma informação da resposta: "Manual dos Monstros, p. 12 — Aarakocra".</summary>
public class FonteConsulta
{
    public int Numero { get; set; }

    /// <summary>Nome por extenso ("Livro do Jogador").</summary>
    public string Livro { get; set; } = string.Empty;

    public int? PaginaInicio { get; set; }

    public int? PaginaFim { get; set; }

    /// <summary>Criatura, magia ou item mágico; nulo quando o trecho é prosa comum.</summary>
    public string? NomeEntidade { get; set; }
}
