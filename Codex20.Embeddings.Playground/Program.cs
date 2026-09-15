using System.Diagnostics;
using System.Reflection;
using System.Text;
using Codex20.Core.Busca;
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
          --lote N            chunks por chamada à API (default 32)
          --limite N          só os N primeiros chunks (teste barato); o livro fica só com eles
          --indexar-bm25      não gera vetores: monta o índice BM25 (FTS5) do livro; não chama a API
          --compactar         reescreve o banco sem o espaço morto da vec0 (o livro informado é ignorado)
          --buscar "txt"      não grava nada: lista os chunks mais parecidos
                              (busca no banco inteiro; o livro informado é ignorado)
          --estrategia NOME   cosseno | bm25 | rrf — como o --buscar ranqueia (default cosseno)
          --comparar          com --buscar: roda todas e imprime uma tabela Markdown lado a lado
          --top N             quantos resultados o --buscar mostra (default 5)
        """);
    return 1;
}

string livro = args[0].ToLowerInvariant();
int tamanhoLote = LerInteiro(args, "--lote", 32);
int limite = LerInteiro(args, "--limite", 0);
int top = LerInteiro(args, "--top", 5);
string? consulta = LerTexto(args, "--buscar");
string nomeEstrategia = LerTexto(args, "--estrategia") ?? "cosseno";
bool isComparar = args.Contains("--comparar");
bool isIndexarBm25 = args.Contains("--indexar-bm25");
bool isCompactar = args.Contains("--compactar");

IConfiguration configuracao = new ConfigurationBuilder()
    .AddUserSecrets(Assembly.GetExecutingAssembly())
    .AddEnvironmentVariables()
    .Build();

// Sem credenciais o programa segue: BM25 e a indexação não precisam da API. Só quem embeda falha.
GeradorEmbeddingAzureOpenAI? gerador = null;
string? erroConfiguracao = null;
try
{
    gerador = new GeradorEmbeddingAzureOpenAI(configuracao);
}
catch (InvalidOperationException erro)
{
    erroConfiguracao = erro.Message;
}

var armazenamento = new ArmazenamentoVetorialSqlite(ResolverCaminhoBanco(), "chunks");
var indiceTextual = new IndiceTextualFts5(ResolverCaminhoBanco(), "chunks_fts");
await armazenamento.PrepararAsync();
await indiceTextual.PrepararAsync();

if (isCompactar)
{
    long bytesAntes = new FileInfo(ResolverCaminhoBanco()).Length;
    var relogioCompactacao = Stopwatch.StartNew();
    await armazenamento.CompactarAsync();
    long bytesDepois = new FileInfo(ResolverCaminhoBanco()).Length;

    Console.WriteLine($"[compactar] {bytesAntes / 1e6:F1} MB → {bytesDepois / 1e6:F1} MB ({relogioCompactacao.Elapsed.TotalSeconds:F1}s)");
    return 0;
}

if (gerador is not null)
{
    Console.WriteLine($"[embedding] modelo {gerador.Modelo} ({gerador.Dimensoes} dimensões)");
}

if (consulta is not null)
{
    // A mesma montagem do ServicoBusca, no Painel.
    var cosseno = new EstrategiaBuscaCosseno(armazenamento, gerador);
    var bm25 = new EstrategiaBuscaBm25(indiceTextual);
    var rrf = new EstrategiaBuscaRrf(new List<IEstrategiaBusca> { cosseno, bm25 });
    var estrategias = new List<IEstrategiaBusca> { cosseno, bm25, rrf };

    var escolhidas = new List<IEstrategiaBusca>();
    if (isComparar)
    {
        escolhidas.AddRange(estrategias);
    }
    else
    {
        IEstrategiaBusca? escolhida = null;
        foreach (IEstrategiaBusca estrategia in estrategias)
        {
            if (estrategia.Nome == nomeEstrategia)
            {
                escolhida = estrategia;
            }
        }

        if (escolhida is null)
        {
            Console.Error.WriteLine($"Estratégia desconhecida: {nomeEstrategia} (use cosseno, bm25 ou rrf).");
            return 1;
        }

        if (!escolhida.IsDisponivel)
        {
            Console.Error.WriteLine(erroConfiguracao ?? escolhida.MotivoIndisponivel);
            Console.Error.WriteLine("Veja Codex20.Embeddings/README.md para o passo a passo, ou use --estrategia bm25.");
            return 1;
        }

        escolhidas.Add(escolhida);
    }

    // Em série, como no Painel: em paralelo as estratégias disputariam CPU e o arquivo do banco.
    var respostas = new List<RespostaBusca>();
    foreach (IEstrategiaBusca estrategia in escolhidas)
    {
        if (!estrategia.IsDisponivel)
        {
            respostas.Add(new RespostaBusca
            {
                NomeEstrategia = estrategia.Nome,
                RotuloPontuacao = estrategia.RotuloPontuacao,
                Observacao = estrategia.MotivoIndisponivel,
            });
            continue;
        }

        var relogioBusca = Stopwatch.StartNew();
        RespostaBusca resposta = await estrategia.BuscarAsync(new PedidoBusca { Texto = consulta, Quantidade = top });
        resposta.Duracao = relogioBusca.Elapsed;
        respostas.Add(resposta);
    }

    if (isComparar)
    {
        ImprimirComparacao(consulta, respostas, top);
    }
    else
    {
        ImprimirResposta(consulta, respostas[0]);
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

string etapa = isIndexarBm25 ? "[bm25]     " : "[embedding]";
Console.WriteLine($"{etapa} {chunks.Count} chunks de {caminhoChunks}"
    + (vazios.Count > 0 ? $" ({vazios.Count} vazio(s) ignorado(s))" : string.Empty));

if (isParcial)
{
    Console.WriteLine($"[aviso]     --limite: o livro '{livro}' será substituído por esses {chunks.Count} chunks apenas");
}

var relogio = Stopwatch.StartNew();

if (isIndexarBm25)
{
    int indexados = await indiceTextual.IndexarAsync(chunks);
    relogio.Stop();
    Console.WriteLine($"[bm25]      {indexados} chunks indexados em {ResolverCaminhoBanco()} ({relogio.Elapsed.TotalSeconds:F1}s)");
    return 0;
}

if (gerador is null)
{
    Console.Error.WriteLine(erroConfiguracao);
    Console.Error.WriteLine("Veja Codex20.Embeddings/README.md para o passo a passo.");
    return 1;
}

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

static void ImprimirResposta(string consulta, RespostaBusca resposta)
{
    Console.WriteLine($"[buscar]    {resposta.NomeEstrategia} \"{consulta}\" — {resposta.Resultados.Count} resultado(s) em {resposta.Duracao.TotalMilliseconds:F1} ms");
    Console.WriteLine($"            pontuação: {resposta.RotuloPontuacao}");
    if (resposta.Observacao is not null)
    {
        Console.WriteLine($"[aviso]     {resposta.Observacao}");
    }

    Console.WriteLine();

    foreach (ResultadoBusca achado in resposta.Resultados)
    {
        Chunk chunk = achado.Chunk;
        string origem = achado.Contribuicoes.Count > 0 ? $"  ({DescreverContribuicoes(achado)})" : string.Empty;
        Console.WriteLine($"── #{achado.Posicao} {achado.Pontuacao:F4}  {TituloChunk(chunk)}  [{chunk.Livro}, p.{chunk.PaginaInicio}]{origem}");
        Console.WriteLine(chunk.Texto);
        Console.WriteLine();
    }
}

// Tabela Markdown pronta para colar no texto do trabalho: uma coluna por estratégia.
static void ImprimirComparacao(string consulta, List<RespostaBusca> respostas, int top)
{
    Console.WriteLine($"Comparação para \"{consulta}\" (top {top})");
    Console.WriteLine();

    var cabecalho = new StringBuilder("| # |");
    var separador = new StringBuilder("| --- |");
    foreach (RespostaBusca resposta in respostas)
    {
        cabecalho.Append($" {resposta.NomeEstrategia} |");
        separador.Append(" --- |");
    }

    Console.WriteLine(cabecalho);
    Console.WriteLine(separador);

    for (int posicao = 0; posicao < top; posicao++)
    {
        var linha = new StringBuilder($"| {posicao + 1} |");
        foreach (RespostaBusca resposta in respostas)
        {
            if (posicao < resposta.Resultados.Count)
            {
                ResultadoBusca achado = resposta.Resultados[posicao];
                string titulo = TituloChunk(achado.Chunk).Replace("|", "\\|");
                linha.Append($" {titulo} ({achado.Chunk.Livro}) · {achado.Pontuacao:F4} |");
            }
            else
            {
                linha.Append(" — |");
            }
        }

        Console.WriteLine(linha);
    }

    var tempos = new StringBuilder("| tempo |");
    foreach (RespostaBusca resposta in respostas)
    {
        tempos.Append($" {resposta.Duracao.TotalMilliseconds:F1} ms |");
    }

    Console.WriteLine(tempos);
    Console.WriteLine();

    foreach (RespostaBusca resposta in respostas)
    {
        Console.WriteLine($"- {resposta.NomeEstrategia}: {resposta.RotuloPontuacao}");
        if (resposta.Observacao is not null)
        {
            Console.WriteLine($"  aviso: {resposta.Observacao}");
        }
    }
}

static string TituloChunk(Chunk chunk)
{
    return chunk.NomeEntidade ?? (chunk.IsFallback ? "(fallback)" : "(sem nome)");
}

static string DescreverContribuicoes(ResultadoBusca resultado)
{
    var partes = new List<string>();
    foreach (ContribuicaoEstrategia contribuicao in resultado.Contribuicoes)
    {
        string posicao = contribuicao.Posicao > 0 ? $"#{contribuicao.Posicao}" : "—";
        partes.Add($"{contribuicao.NomeEstrategia} {posicao}");
    }

    return string.Join(" · ", partes);
}

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
