using System.Reflection;
using System.Text;
using System.Text.Json;
using Codex20.Avaliacao;
using Codex20.Avaliacao.Gabarito;
using Codex20.Avaliacao.Registros;
using Codex20.Core.Busca;
using Codex20.Core.Chunking;
using Codex20.Core.Geracao;
using Codex20.Embeddings;
using Codex20.Geracao;
using Microsoft.Extensions.Configuration;

Console.OutputEncoding = Encoding.UTF8;

const int QuantidadeTrechos = 8;

if (args.Length < 1)
{
    Console.WriteLine("""
        Uso: Codex20.Avaliacao <comando> [opções]

        Comandos:
          validar               confere o CSV contra os *.chunks.json atuais; não chama a API
          rodar                 roda a avaliação e grava Codex20.Avaliacao/Resultados/avaliacao-<data>.jsonl
          pagina <livro> <n>    lista os chunks de uma página, com o Id (para montar o gabarito)
          localizar "texto"     lista os chunks que contêm o texto, sem ranquear (para montar o gabarito)

        Opções:
          --perguntas CAMINHO   CSV de perguntas (default Codex20.Avaliacao/Dados/perguntas.csv)
          --ids q01,q02         rodar: só essas perguntas
          --repeticoes N        rodar: gera N vezes cada resposta, com e sem RAG (default 1)
          --profundidade N      rodar: até que posição procurar o chunk de referência (default 50)
          --continuar ARQUIVO   rodar: acrescenta a um JSONL já começado, pulando as perguntas que ele tem
          --livro NOME          localizar: só num livro (jogador, mestre, monstro)
        """);
    return 1;
}

string comando = args[0].ToLowerInvariant();
string caminhoPerguntas = LerTexto(args, "--perguntas") ?? ArquivosAvaliacao.CaminhoPerguntasPadrao();

List<Chunk> chunks;
try
{
    chunks = ArquivosAvaliacao.LerChunks();
}
catch (FileNotFoundException erro)
{
    Console.Error.WriteLine(erro.Message);
    return 1;
}

if (comando == "pagina")
{
    if (args.Length < 3 || !int.TryParse(args[2], out int pagina))
    {
        Console.Error.WriteLine("Uso: pagina <jogador|mestre|monstro> <número da página>");
        return 1;
    }

    string livro = args[1].ToLowerInvariant();
    var achados = new List<Chunk>();
    foreach (Chunk chunk in chunks)
    {
        if (chunk.Livro == livro && chunk.PaginaInicio is not null
            && chunk.PaginaInicio <= pagina && pagina <= (chunk.PaginaFim ?? chunk.PaginaInicio))
        {
            achados.Add(chunk);
        }
    }

    ImprimirChunks(achados, $"{livro}, p. {pagina}");
    return 0;
}

if (comando == "localizar")
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Uso: localizar \"texto\" [--livro jogador|mestre|monstro]");
        return 1;
    }

    // Sem acento e sem caixa dos dois lados: "espaços de magia" acha "Espaços de Magia".
    string procurado = LeitorPerguntas.Normalizar(args[1]);
    string? livro = LerTexto(args, "--livro")?.ToLowerInvariant();
    var achados = new List<Chunk>();
    foreach (Chunk chunk in chunks)
    {
        if (livro is not null && chunk.Livro != livro)
        {
            continue;
        }

        if (LeitorPerguntas.Normalizar(chunk.Texto).Contains(procurado))
        {
            achados.Add(chunk);
        }
    }

    ImprimirChunks(achados, $"\"{args[1]}\"");
    return 0;
}

if (comando != "validar" && comando != "rodar")
{
    Console.Error.WriteLine($"Comando desconhecido: {comando} (use validar, rodar, pagina ou localizar).");
    return 1;
}

if (!File.Exists(caminhoPerguntas))
{
    Console.Error.WriteLine($"CSV de perguntas não encontrado: {caminhoPerguntas}");
    Console.Error.WriteLine("Veja o formato em Codex20.Avaliacao/README.md e o exemplo em Codex20.Avaliacao/Dados/perguntas.exemplo.csv.");
    return 1;
}

// Texto repetido no mesmo livro tem o mesmo Id (são 9 casos): o primeiro basta.
var chunksPorId = new Dictionary<Guid, Chunk>();
var chunksPorLivro = new Dictionary<string, int>();
foreach (Chunk chunk in chunks)
{
    chunksPorId.TryAdd(chunk.Id, chunk);
    chunksPorLivro[chunk.Livro] = chunksPorLivro.GetValueOrDefault(chunk.Livro) + 1;
}

var problemas = new ProblemasGabarito();
// As linhas com erro de formato já ficaram de fora; conferir as outras mesmo assim mostra todos os
// problemas de uma vez, em vez de um por rodada.
List<PerguntaAvaliacao> perguntas = LeitorPerguntas.Ler(caminhoPerguntas, problemas);
ValidadorGabarito.Validar(perguntas, chunksPorId, problemas);

ImprimirProblemas(problemas);
if (!problemas.IsValido)
{
    return 1;
}

if (comando == "validar")
{
    ImprimirResumo(perguntas, caminhoPerguntas);
    return 0;
}

// --- rodar ---

int repeticoes = Math.Max(1, LerInteiro(args, "--repeticoes", 1));
int profundidade = Math.Max(QuantidadeTrechos, LerInteiro(args, "--profundidade", 50));
string? caminhoContinuar = LerTexto(args, "--continuar");
string? filtroIds = LerTexto(args, "--ids");

if (filtroIds is not null)
{
    var ids = new HashSet<string>(filtroIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    var filtradas = new List<PerguntaAvaliacao>();
    foreach (PerguntaAvaliacao pergunta in perguntas)
    {
        if (ids.Remove(pergunta.Id))
        {
            filtradas.Add(pergunta);
        }
    }

    if (ids.Count > 0)
    {
        Console.Error.WriteLine($"Id(s) que não estão no CSV: {string.Join(", ", ids)}");
        return 1;
    }

    perguntas = filtradas;
}

IConfiguration configuracao = new ConfigurationBuilder()
    .AddUserSecrets(Assembly.GetExecutingAssembly())
    .AddEnvironmentVariables()
    .Build();

GeradorEmbeddingAzureOpenAI geradorEmbedding;
GeradorRespostaAzureOpenAI geradorResposta;
try
{
    geradorEmbedding = new GeradorEmbeddingAzureOpenAI(configuracao);
    geradorResposta = new GeradorRespostaAzureOpenAI(configuracao);
}
catch (InvalidOperationException erro)
{
    Console.Error.WriteLine(erro.Message);
    Console.Error.WriteLine("Os user-secrets são os mesmos do Painel; veja Codex20.Embeddings/README.md e Codex20.Geracao/README.md.");
    return 1;
}

var armazenamento = new ArmazenamentoVetorialSqlite(ArquivosAvaliacao.CaminhoBanco(), "chunks");
var indiceTextual = new IndiceTextualFts5(ArquivosAvaliacao.CaminhoBanco(), "chunks_fts");
await armazenamento.PrepararAsync();
await indiceTextual.PrepararAsync();

// A mesma montagem do ServicoBusca (Painel) e do ServicoConsulta (Api).
var cosseno = new EstrategiaBuscaCosseno(armazenamento, geradorEmbedding);
var bm25 = new EstrategiaBuscaBm25(indiceTextual);
var rrf = new EstrategiaBuscaRrf(new List<IEstrategiaBusca> { cosseno, bm25 });
var pipeline = new PipelineRag(geradorResposta);
var executor = new ExecutorAvaliacao(new List<IEstrategiaBusca> { cosseno, bm25, rrf }, rrf, pipeline,
    profundidade, QuantidadeTrechos, repeticoes);

string caminhoSaida;
var jaGravadas = new HashSet<string>();
if (caminhoContinuar is not null)
{
    caminhoSaida = Path.GetFullPath(caminhoContinuar);
    if (!File.Exists(caminhoSaida))
    {
        Console.Error.WriteLine($"Arquivo para continuar não encontrado: {caminhoSaida}");
        return 1;
    }

    // Juntar respostas de dois modelos no mesmo arquivo estragaria a comparação sem ninguém perceber.
    string caminhoMeta = ArquivosAvaliacao.CaminhoMeta(caminhoSaida);
    if (File.Exists(caminhoMeta))
    {
        MetadadosAvaliacao? anterior = JsonSerializer.Deserialize<MetadadosAvaliacao>(File.ReadAllText(caminhoMeta));
        if (anterior is not null && anterior.Modelo != pipeline.Modelo)
        {
            Console.Error.WriteLine($"O arquivo foi gerado com {anterior.Modelo}, e o modelo configurado agora é {pipeline.Modelo}. Comece um arquivo novo.");
            return 1;
        }
    }

    jaGravadas = ArquivosAvaliacao.LerIdsGravados(caminhoSaida);
}
else
{
    Directory.CreateDirectory(ArquivosAvaliacao.PastaResultados());
    caminhoSaida = Path.Combine(ArquivosAvaliacao.PastaResultados(), $"avaliacao-{DateTime.Now:yyyyMMdd-HHmmss}.jsonl");

    var metadados = new MetadadosAvaliacao
    {
        DataHora = DateTime.Now,
        Modelo = pipeline.Modelo,
        ModeloEmbedding = geradorEmbedding.Modelo,
        EstrategiaGeracao = rrf.Nome,
        QuantidadeTrechos = QuantidadeTrechos,
        Profundidade = profundidade,
        Repeticoes = repeticoes,
        ArquivoPerguntas = Path.GetFullPath(caminhoPerguntas),
        QuantidadePerguntas = perguntas.Count,
        ChunksPorLivro = chunksPorLivro,
        InstrucoesComRag = MontadorPrompt.Instrucoes,
        InstrucoesSemRag = MontadorPrompt.InstrucoesSemRag,
    };
    File.WriteAllText(ArquivosAvaliacao.CaminhoMeta(caminhoSaida), JsonSerializer.Serialize(metadados, ArquivosAvaliacao.OpcoesMeta));
}

var pendentes = new List<PerguntaAvaliacao>();
foreach (PerguntaAvaliacao pergunta in perguntas)
{
    if (!jaGravadas.Contains(pergunta.Id))
    {
        pendentes.Add(pergunta);
    }
}

Console.WriteLine($"[avaliacao] modelo {pipeline.Modelo} · embedding {geradorEmbedding.Modelo}");
Console.WriteLine($"[avaliacao] {pendentes.Count} pergunta(s)"
    + (jaGravadas.Count > 0 ? $" ({jaGravadas.Count} já gravada(s), puladas)" : string.Empty)
    + $" · {repeticoes} rodada(s) · ~{pendentes.Count * repeticoes * 2} chamada(s) ao modelo");
Console.WriteLine($"[avaliacao] gravando em {caminhoSaida}");
Console.WriteLine();

int tokensEntrada = 0;
int tokensSaida = 0;

using (var saida = new StreamWriter(caminhoSaida, append: true, new UTF8Encoding(false)))
{
    // Linha por linha no disco: se a API falhar no meio, o que já rodou não se perde.
    saida.AutoFlush = true;

    for (int i = 0; i < pendentes.Count; i++)
    {
        PerguntaAvaliacao pergunta = pendentes[i];
        RegistroPergunta registro;
        try
        {
            registro = await executor.AvaliarAsync(pergunta);
        }
        catch (Exception erro)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"Erro na pergunta {pergunta.Id}: {erro.Message}");
            Console.Error.WriteLine($"As {i} anteriores estão gravadas. Para retomar: rodar --continuar \"{caminhoSaida}\"");
            return 1;
        }

        saida.WriteLine(JsonSerializer.Serialize(registro, ArquivosAvaliacao.OpcoesJsonl));

        foreach (RegistroGeracao geracao in registro.Geracoes)
        {
            tokensEntrada += geracao.TokensEntrada;
            tokensSaida += geracao.TokensSaida;
        }

        Console.WriteLine($"[{i + 1}/{pendentes.Count}] {pergunta.Id,-6} {DescreverRegistro(registro)}");
    }
}

Console.WriteLine();
Console.WriteLine($"[avaliacao] pronto: {tokensEntrada} tokens de entrada + {tokensSaida} de saída (inclui raciocínio)");
Console.WriteLine($"[avaliacao] {caminhoSaida}");
return 0;

static string DescreverRegistro(RegistroPergunta registro)
{
    var partes = new List<string>();
    foreach (RegistroRecuperacao recuperacao in registro.Recuperacao)
    {
        string posicao;
        if (!registro.IsRespondivel)
        {
            posicao = "n/a";
        }
        else if (recuperacao.PosicaoPrimeiraReferencia is null)
        {
            posicao = "—";
        }
        else
        {
            posicao = $"#{recuperacao.PosicaoPrimeiraReferencia}";
        }

        partes.Add($"{recuperacao.Estrategia} {posicao}");
    }

    foreach (RegistroGeracao geracao in registro.Geracoes)
    {
        if (geracao.Rodada > 1)
        {
            continue;
        }

        string detalhe = geracao.Modo == RegistroGeracao.ModoComRag
            ? $"{geracao.Citacoes.Count} citação(ões)"
            : $"{geracao.LivrosCitados.Count} livro(s) e {geracao.PaginasCitadas.Count} página(s) citado(s)";
        partes.Add($"{geracao.Modo} {detalhe}, {geracao.DuracaoGeracaoMs / 1000:F1} s{(geracao.IsCortada ? " CORTADA" : string.Empty)}");
    }

    return string.Join(" · ", partes);
}

static void ImprimirProblemas(ProblemasGabarito problemas)
{
    foreach (string erro in problemas.Erros)
    {
        Console.Error.WriteLine($"[erro]  {erro}");
    }

    foreach (string aviso in problemas.Avisos)
    {
        Console.WriteLine($"[aviso] {aviso}");
    }

    if (problemas.Erros.Count > 0)
    {
        Console.Error.WriteLine($"{problemas.Erros.Count} erro(s) no gabarito; corrija o CSV antes de rodar.");
    }
}

static void ImprimirResumo(List<PerguntaAvaliacao> perguntas, string caminho)
{
    var porTipo = new SortedDictionary<string, int>();
    var porLivro = new SortedDictionary<string, int>();
    int respondiveis = 0;
    foreach (PerguntaAvaliacao pergunta in perguntas)
    {
        porTipo[pergunta.Tipo] = porTipo.GetValueOrDefault(pergunta.Tipo) + 1;
        porLivro[pergunta.Livro] = porLivro.GetValueOrDefault(pergunta.Livro) + 1;
        if (pergunta.IsRespondivel)
        {
            respondiveis++;
        }
    }

    Console.WriteLine($"[validar] {perguntas.Count} pergunta(s) em {caminho}: gabarito ok");
    Console.WriteLine($"          {respondiveis} respondível(is), {perguntas.Count - respondiveis} sem resposta nos livros");
    Console.WriteLine($"          por tipo:  {DescreverContagem(porTipo)}");
    Console.WriteLine($"          por livro: {DescreverContagem(porLivro)}");
}

static string DescreverContagem(SortedDictionary<string, int> contagem)
{
    var partes = new List<string>();
    foreach (KeyValuePair<string, int> item in contagem)
    {
        partes.Add($"{(item.Key.Length == 0 ? "(vazio)" : item.Key)} {item.Value}");
    }

    return string.Join(" · ", partes);
}

// Uma linha por chunk: Id para colar no CSV, de onde é, e o começo do texto para reconhecer.
static void ImprimirChunks(List<Chunk> achados, string descricao)
{
    Console.WriteLine($"{achados.Count} chunk(s) para {descricao}");
    Console.WriteLine();

    int limite = 40;
    for (int i = 0; i < achados.Count && i < limite; i++)
    {
        Chunk chunk = achados[i];
        string nome = chunk.NomeEntidade ?? (chunk.IsFallback ? "(fallback)" : "(sem nome)");
        string inicio = string.Join(' ', chunk.Texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (inicio.Length > 160)
        {
            inicio = inicio[..160] + "…";
        }

        Console.WriteLine($"{chunk.Id}  {chunk.Livro} p.{ValidadorGabarito.DescreverPaginas(chunk)}  {nome}");
        Console.WriteLine($"    {inicio}");
    }

    if (achados.Count > limite)
    {
        Console.WriteLine($"… e mais {achados.Count - limite}. Use um texto mais específico (ou --livro).");
    }
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
