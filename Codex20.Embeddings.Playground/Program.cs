using System.Diagnostics;
using System.Reflection;
using System.Text;
using Codex20.Core.Chunking;
using Codex20.Core.Embeddings;
using Codex20.Embeddings;
using Microsoft.Extensions.Configuration;

Console.OutputEncoding = Encoding.UTF8;

if (args.Length < 1)
{
    Console.WriteLine("""
        Uso: Codex20.Embeddings.Playground <monstro|jogador|mestre> [opções]
          (lê Codex20.Ingestion/Data/Chunks/<livro>.chunks.json, gerado pelo --save do Chunking.Playground)
          --lote N        chunks por chamada à API (default 32)
          --limite N      só os N primeiros chunks (teste barato); o livro fica só com eles
          --buscar "txt"  não grava nada: embeda o texto e lista os chunks mais parecidos
                          (busca no banco inteiro; o livro informado é ignorado)
          --top N         quantos resultados o --buscar mostra (default 5)
        """);
    return 1;
}

string livro = args[0].ToLowerInvariant();
int tamanhoLote = LerInteiro(args, "--lote", 32);
int limite = LerInteiro(args, "--limite", 0);
int top = LerInteiro(args, "--top", 5);
string? consulta = LerTexto(args, "--buscar");

IConfiguration configuracao = new ConfigurationBuilder()
    .AddUserSecrets(Assembly.GetExecutingAssembly())
    .AddEnvironmentVariables()
    .Build();

GeradorEmbeddingAzureOpenAI gerador;
try
{
    gerador = new GeradorEmbeddingAzureOpenAI(configuracao);
}
catch (InvalidOperationException erro)
{
    Console.Error.WriteLine(erro.Message);
    Console.Error.WriteLine("Veja Codex20.Embeddings/README.md para o passo a passo.");
    return 1;
}

var armazenamento = new ArmazenamentoVetorialSqlite(ResolverCaminhoBanco(), "chunks");
await armazenamento.PrepararAsync();

Console.WriteLine($"[embedding] modelo {gerador.Modelo} ({gerador.Dimensoes} dimensões)");

if (consulta is not null)
{
    List<float[]> vetorConsulta = await gerador.GerarEmLoteAsync(new List<string> { consulta });
    List<ResultadoBusca> achados = await armazenamento.BuscarSimilaresAsync(vetorConsulta[0], top);

    Console.WriteLine($"[buscar]    \"{consulta}\" — {achados.Count} resultado(s)");
    Console.WriteLine();

    foreach (ResultadoBusca achado in achados)
    {
        Chunk chunk = achado.Chunk;
        string titulo = chunk.NomeEntidade ?? (chunk.IsFallback ? "(fallback)" : "(sem nome)");
        Console.WriteLine($"── {achado.Similaridade:F4}  {titulo}  [{chunk.Livro}, p.{chunk.PaginaInicio}]");
        Console.WriteLine(chunk.Texto);
        Console.WriteLine();
    }

    return 0;
}

string caminhoChunks = ResolverCaminhoChunks(livro);
if (!File.Exists(caminhoChunks))
{
    Console.Error.WriteLine($"Arquivo de chunks não encontrado: {caminhoChunks}");
    Console.Error.WriteLine("Gere primeiro com: dotnet run --project Codex20.Chunking.Playground -- <livro> --save");
    return 1;
}

List<Chunk> chunks = ArquivoChunks.Ler(caminhoChunks);
List<Chunk> vazios = chunks.Where(c => string.IsNullOrWhiteSpace(c.Texto)).ToList();
chunks = chunks.Where(c => !string.IsNullOrWhiteSpace(c.Texto)).ToList();

bool isParcial = limite > 0 && limite < chunks.Count;
if (isParcial)
{
    chunks = chunks.Take(limite).ToList();
}

Console.WriteLine($"[embedding] {chunks.Count} chunks de {caminhoChunks}"
    + (vazios.Count > 0 ? $" ({vazios.Count} vazio(s) ignorado(s))" : string.Empty));

if (isParcial)
{
    Console.WriteLine($"[aviso]     --limite: o livro '{livro}' será substituído por esses {chunks.Count} chunks apenas");
}

var relogio = Stopwatch.StartNew();
var embeddings = new List<ChunkEmbedding>();

for (int inicio = 0; inicio < chunks.Count; inicio += tamanhoLote)
{
    List<Chunk> lote = chunks.Skip(inicio).Take(tamanhoLote).ToList();
    List<float[]> vetores = await gerador.GerarEmLoteAsync(lote.Select(c => c.Texto).ToList());

    for (int i = 0; i < lote.Count; i++)
    {
        embeddings.Add(new ChunkEmbedding
        {
            ChunkId = lote[i].Id,
            Vetor = vetores[i],
            Modelo = gerador.Modelo,
        });
    }

    Console.WriteLine($"[embedding] {embeddings.Count}/{chunks.Count} chunks ({relogio.Elapsed.TotalSeconds:F1}s)");
}

int gravados = await armazenamento.SalvarAsync(chunks, embeddings);
relogio.Stop();

string repetidos = gravados < embeddings.Count
    ? $" ({embeddings.Count - gravados} de texto repetido gravado(s) uma vez só)"
    : string.Empty;

Console.WriteLine($"[save]      {gravados} vetores gravados em {ResolverCaminhoBanco()}{repetidos} ({relogio.Elapsed.TotalSeconds:F1}s)");
return 0;

static string ResolverCaminhoChunks(string livro)
{
    return Path.Combine(ResolverPastaDados(), "Chunks", $"{livro}.chunks.json");
}

static string ResolverCaminhoBanco()
{
    return Path.Combine(ResolverPastaDados(), "Vetores", "embeddings.db");
}

// Sobe a partir do binário até achar Codex20.Ingestion, que é onde ficam os dados do pipeline.
static string ResolverPastaDados()
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

static int LerInteiro(string[] args, string flag, int padrao)
{
    int idx = Array.IndexOf(args, flag);
    return idx >= 0 && idx + 1 < args.Length && int.TryParse(args[idx + 1], out int v) ? v : padrao;
}

static string? LerTexto(string[] args, string flag)
{
    int idx = Array.IndexOf(args, flag);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}