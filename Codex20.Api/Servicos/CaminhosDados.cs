namespace Codex20.Api.Servicos;

/// <summary>
/// Onde fica o banco. Sobe a partir do binário até achar Codex20.Ingestion, igual ao Painel: a Api
/// roda na máquina de desenvolvimento, exposta pelo ngrok, e lê o mesmo arquivo que o Painel grava.
/// </summary>
public static class CaminhosDados
{
    public static string Banco()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string ingestion = Path.Combine(dir.FullName, "Codex20.Ingestion");
            if (Directory.Exists(ingestion))
            {
                return Path.Combine(ingestion, "Data", "Vetores", "embeddings.db");
            }
        }

        throw new DirectoryNotFoundException("Não encontrei a pasta Codex20.Ingestion.");
    }
}
