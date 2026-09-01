using System.Text;
using Codex20.Chunking.Playground;
using Codex20.Core.Chunking;
using Codex20.Core.PreProcessamento;

Console.OutputEncoding = Encoding.UTF8;

if (args.Length < 1)
{
    Console.WriteLine("""
        Uso: Codex20.Chunking.Playground <monstro|jogador|mestre> [caminho.md] [opções]
          (sem caminho, usa o RESULTADO_*.md revisado em Codex20.Ingestion/Data/Markdown)
          --slice A,B     processa apenas as linhas [A,B] do Markdown (1-based, inclusivo)
          --boundaries    imprime DespejarFronteirasEntidade (fronteiras entre chunks)
          --sample N      mostra as N primeiras entidades em detalhe (default 0)
          --show-fallback inclui chunks de fallback no dump de fronteiras
        """);
    return 1;
}

string livro = args[0].ToLowerInvariant();
string? caminho = args.Length >= 2 && !args[1].StartsWith("--") ? args[1] : ResolverCaminhoPadrao(livro);
(int recorteInicio, int recorteFim) = LerRecorte(args);
bool fronteiras = args.Contains("--boundaries");
bool mostrarFallback = args.Contains("--show-fallback");
int amostra = LerInteiro(args, "--sample", 0);

if (caminho is null || !File.Exists(caminho))
{
    Console.Error.WriteLine($"Arquivo não encontrado: {caminho ?? "(livro desconhecido)"}");
    return 1;
}

string markdown = File.ReadAllText(caminho);
if (recorteInicio > 0 || recorteFim > 0)
{
    string[] todas = markdown.Replace("\r\n", "\n").Split('\n');
    int a = Math.Max(1, recorteInicio) - 1;
    int b = recorteFim > 0 ? Math.Min(todas.Length, recorteFim) : todas.Length;
    markdown = string.Join('\n', todas[a..b]);
    Console.WriteLine($"[slice] linhas {a + 1}..{b} ({b - a} linhas)");
}

var preProcessador = new PreProcessadorDocumentoMarkdown();
List<BlocoDocumento> blocos = preProcessador.Processar(markdown);

var fallback = new ChunkingStrategyParagrafoToken();
ChunkingStrategyPorEntidade strategy = livro switch
{
    "monstro" or "monstros" or "manual" => ChunkingStrategyPorEntidade.ParaManualDosMonstros(fallback),
    "jogador" or "livro" or "phb" => ChunkingStrategyPorEntidade.ParaLivroDoJogador(fallback),
    "mestre" or "guia" or "dmg" => ChunkingStrategyPorEntidade.ParaGuiaDoMestre(fallback),
    _ => throw new ArgumentException($"Livro desconhecido: {livro}"),
};

Console.WriteLine($"[preprocess] {blocos.Count} blocos "
    + $"({blocos.Count(b => b is BlocoParagrafo)} parágrafos, {blocos.Count(b => b is BlocoTabela)} tabelas)");
Console.WriteLine($"[strategy]   {strategy.Nome}");
Console.WriteLine();

List<Chunk> chunks = strategy.Chunk(blocos, livro);

RelatorioEntidade.ImprimirEstatisticas(chunks);

if (amostra > 0)
{
    RelatorioEntidade.ImprimirAmostra(chunks, amostra);
}

if (fronteiras)
{
    RelatorioEntidade.DespejarFronteirasEntidade(chunks, mostrarFallback);
}

return 0;

// Procura o RESULTADO_*.md revisado subindo a partir do binário até achar a raiz da solução.
static string? ResolverCaminhoPadrao(string livro)
{
    string? arquivo = livro switch
    {
        "monstro" or "monstros" or "manual" => "RESULTADO_manualMonstro.md",
        "jogador" or "livro" or "phb" => "RESULTADO_LivroDoJogador.md",
        "mestre" or "guia" or "dmg" => "RESULTADO_GuiaDoMestre.md",
        _ => null,
    };
    if (arquivo is null)
    {
        return null;
    }

    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
    {
        string candidato = Path.Combine(dir.FullName, "Codex20.Ingestion", "Data", "Markdown", arquivo);
        if (File.Exists(candidato))
        {
            return candidato;
        }
    }

    return null;
}

static (int, int) LerRecorte(string[] args)
{
    int idx = Array.IndexOf(args, "--slice");
    if (idx < 0 || idx + 1 >= args.Length)
    {
        return (0, 0);
    }

    string[] partes = args[idx + 1].Split(',', 2);
    return (int.Parse(partes[0]), partes.Length > 1 ? int.Parse(partes[1]) : 0);
}

static int LerInteiro(string[] args, string flag, int fallback)
{
    int idx = Array.IndexOf(args, flag);
    return idx >= 0 && idx + 1 < args.Length && int.TryParse(args[idx + 1], out int v) ? v : fallback;
}
