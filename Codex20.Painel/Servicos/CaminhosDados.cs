namespace Codex20.Painel.Servicos;

/// <summary>
/// Onde ficam os arquivos do pipeline. Sobe a partir do binário até achar Codex20.Ingestion,
/// igual aos Playgrounds, então funciona tanto no dotnet run quanto no Visual Studio.
/// </summary>
public static class CaminhosDados
{
    public static string PastaDados()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string ingestion = Path.Combine(dir.FullName, "Codex20.Ingestion");
            if (Directory.Exists(ingestion))
            {
                return Path.Combine(ingestion, "Data");
            }
        }

        throw new DirectoryNotFoundException("Não encontrei a pasta Codex20.Ingestion.");
    }

    public static string Markdown(string livro)
    {
        return Path.Combine(PastaDados(), "Markdown", CatalogoLivros.ArquivoMarkdown(livro));
    }

    public static string Chunks(string livro)
    {
        return Path.Combine(PastaDados(), "Chunks", $"{livro}.chunks.json");
    }

    public static string Banco()
    {
        return Path.Combine(PastaDados(), "Vetores", "embeddings.db");
    }
}
