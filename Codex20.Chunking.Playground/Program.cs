using System.Text;
using Codex20.Chunking.Playground;
using Codex20.Core.Chunking;
using Codex20.Core.Preprocessing;

Console.OutputEncoding = Encoding.UTF8;

if (args.Length < 1)
{
    Console.WriteLine("""
        Uso: Codex20.Chunking.Playground <monstro|jogador|mestre> [caminho.md] [opções]
          (sem caminho, usa o RESULTADO_*.md revisado em Codex20.Ingestion/Data/Markdown)
          --slice A,B     processa apenas as linhas [A,B] do Markdown (1-based, inclusivo)
          --boundaries    imprime DumpEntityBoundaries (fronteiras entre chunks)
          --sample N      mostra as N primeiras entidades em detalhe (default 0)
          --show-fallback inclui chunks de fallback no dump de fronteiras
        """);
    return 1;
}

string book = args[0].ToLowerInvariant();
string? path = args.Length >= 2 && !args[1].StartsWith("--") ? args[1] : ResolveDefaultPath(book);
(int sliceStart, int sliceEnd) = ParseSlice(args);
bool boundaries = args.Contains("--boundaries");
bool showFallback = args.Contains("--show-fallback");
int sample = ParseInt(args, "--sample", 0);

if (path is null || !File.Exists(path))
{
    Console.Error.WriteLine($"Arquivo não encontrado: {path ?? "(livro desconhecido)"}");
    return 1;
}

string markdown = File.ReadAllText(path);
if (sliceStart > 0 || sliceEnd > 0)
{
    string[] all = markdown.Replace("\r\n", "\n").Split('\n');
    int a = Math.Max(1, sliceStart) - 1;
    int b = sliceEnd > 0 ? Math.Min(all.Length, sliceEnd) : all.Length;
    markdown = string.Join('\n', all[a..b]);
    Console.WriteLine($"[slice] linhas {a + 1}..{b} ({b - a} linhas)");
}

var preprocessor = new MarkdownDocumentPreprocessor();
List<DocumentBlock> blocks = preprocessor.Process(markdown);

var fallback = new ParagraphTokenChunkingStrategy();
EntityAwareChunkingStrategy strategy = book switch
{
    "monstro" or "monstros" or "manual" => EntityAwareChunkingStrategy.ForManualDosMonstros(fallback),
    "jogador" or "livro" or "phb" => EntityAwareChunkingStrategy.ForLivroDoJogador(fallback),
    "mestre" or "guia" or "dmg" => EntityAwareChunkingStrategy.ForGuiaDoMestre(fallback),
    _ => throw new ArgumentException($"Livro desconhecido: {book}"),
};

Console.WriteLine($"[preprocess] {blocks.Count} blocos "
    + $"({blocks.Count(b => b is ParagraphBlock)} parágrafos, {blocks.Count(b => b is TableBlock)} tabelas)");
Console.WriteLine($"[strategy]   {strategy.Name}");
Console.WriteLine();

List<Chunk> chunks = strategy.Chunk(blocks, book);

EntityReport.PrintStats(chunks);

if (sample > 0)
{
    EntityReport.PrintSample(chunks, sample);
}

if (boundaries)
{
    EntityReport.DumpEntityBoundaries(chunks, showFallback);
}

return 0;

// Procura o RESULTADO_*.md revisado subindo a partir do binário até achar a raiz da solução.
static string? ResolveDefaultPath(string book)
{
    string? file = book switch
    {
        "monstro" or "monstros" or "manual" => "RESULTADO_manualMonstro.md",
        "jogador" or "livro" or "phb" => "RESULTADO_LivroDoJogador.md",
        "mestre" or "guia" or "dmg" => "RESULTADO_GuiaDoMestre.md",
        _ => null,
    };
    if (file is null)
    {
        return null;
    }

    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
    {
        string candidate = Path.Combine(dir.FullName, "Codex20.Ingestion", "Data", "Markdown", file);
        if (File.Exists(candidate))
        {
            return candidate;
        }
    }

    return null;
}

static (int, int) ParseSlice(string[] args)
{
    int idx = Array.IndexOf(args, "--slice");
    if (idx < 0 || idx + 1 >= args.Length)
    {
        return (0, 0);
    }

    string[] parts = args[idx + 1].Split(',', 2);
    return (int.Parse(parts[0]), parts.Length > 1 ? int.Parse(parts[1]) : 0);
}

static int ParseInt(string[] args, string flag, int fallback)
{
    int idx = Array.IndexOf(args, flag);
    return idx >= 0 && idx + 1 < args.Length && int.TryParse(args[idx + 1], out int v) ? v : fallback;
}
