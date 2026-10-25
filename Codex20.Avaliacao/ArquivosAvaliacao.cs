using System.Text.Encodings.Web;
using System.Text.Json;
using Codex20.Core.Chunking;

namespace Codex20.Avaliacao;

/// <summary>Onde ficam os dados que a avaliação lê e grava, e o formato do JSON de saída.</summary>
public static class ArquivosAvaliacao
{
    public static readonly string[] Livros = { "jogador", "mestre", "monstro" };

    /// <summary>Uma pergunta por linha: dá para acompanhar o arquivo crescendo e retomar do ponto onde parou.</summary>
    public static readonly JsonSerializerOptions OpcoesJsonl = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static readonly JsonSerializerOptions OpcoesMeta = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>A pasta da solução: sobe a partir do binário até achar Codex20.Ingestion, igual aos Playgrounds.</summary>
    public static string Raiz()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Codex20.Ingestion")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("Não encontrei a pasta Codex20.Ingestion.");
    }

    public static string CaminhoBanco()
    {
        return Path.Combine(Raiz(), "Codex20.Ingestion", "Data", "Vetores", "embeddings.db");
    }

    public static string CaminhoChunks(string livro)
    {
        return Path.Combine(Raiz(), "Codex20.Ingestion", "Data", "Chunks", $"{livro}.chunks.json");
    }

    public static string CaminhoPerguntasPadrao()
    {
        return Path.Combine(Raiz(), "Codex20.Avaliacao", "Dados", "perguntas.csv");
    }

    public static string PastaResultados()
    {
        return Path.Combine(Raiz(), "Codex20.Avaliacao", "Resultados");
    }

    /// <summary>"avaliacao-20261018-1530.jsonl" → "avaliacao-20261018-1530.meta.json".</summary>
    public static string CaminhoMeta(string caminhoJsonl)
    {
        return Path.ChangeExtension(caminhoJsonl, ".meta.json");
    }

    /// <summary>Os chunks dos três livros, na ordem dos arquivos.</summary>
    public static List<Chunk> LerChunks()
    {
        var chunks = new List<Chunk>();
        foreach (string livro in Livros)
        {
            string caminho = CaminhoChunks(livro);
            if (!File.Exists(caminho))
            {
                throw new FileNotFoundException(
                    $"Arquivo de chunks não encontrado: {caminho}. Gere com: dotnet run --project Codex20.Chunking.Playground -- {livro} --save");
            }

            chunks.AddRange(ArquivoChunks.Ler(caminho));
        }

        return chunks;
    }

    /// <summary>Ids das perguntas já gravadas num JSONL, para o <c>--continuar</c> pular.</summary>
    public static HashSet<string> LerIdsGravados(string caminhoJsonl)
    {
        var ids = new HashSet<string>();
        foreach (string linha in File.ReadAllLines(caminhoJsonl))
        {
            if (string.IsNullOrWhiteSpace(linha))
            {
                continue;
            }

            using JsonDocument documento = JsonDocument.Parse(linha);
            string? id = documento.RootElement.GetProperty("Id").GetString();
            if (id is not null)
            {
                ids.Add(id);
            }
        }

        return ids;
    }
}
