namespace Codex20.Api.Servicos;

/// <summary>Nomes das políticas de rate limiting registradas no Program.cs.</summary>
public static class PoliticasLimite
{
    /// <summary>Poucas perguntas por minuto por IP: segura script e clique repetido.</summary>
    public const string PorIp = "consultas-por-ip";
}
