using Codex20.Core.Chunking;
using Codex20.Core.Embeddings;
using Codex20.Embeddings;

namespace Codex20.Painel.Servicos;

/// <summary>
/// Acesso ao gerador de embeddings e ao banco vetorial. Sem credenciais do Azure OpenAI o
/// Painel continua abrindo: só as telas que chamam a API ficam bloqueadas, com a mensagem de
/// <see cref="ErroConfiguracao"/>.
/// </summary>
public class ServicoEmbeddings
{
    private readonly GeradorEmbeddingAzureOpenAI? gerador;
    private readonly ArmazenamentoVetorialSqlite armazenamento;
    private bool isPreparado;

    public ServicoEmbeddings(IConfiguration configuracao)
    {
        try
        {
            gerador = new GeradorEmbeddingAzureOpenAI(configuracao);
        }
        catch (InvalidOperationException erro)
        {
            ErroConfiguracao = erro.Message;
        }

        armazenamento = new ArmazenamentoVetorialSqlite(CaminhosDados.Banco(), "chunks");
    }

    public string? ErroConfiguracao { get; }

    public bool IsConfigurado => gerador is not null;

    public string Modelo => gerador is null ? "(não configurado)" : gerador.Modelo;

    /// <summary>Os chunks do <c>.chunks.json</c> que vão para a API (texto vazio fica de fora).</summary>
    public List<Chunk> LerChunksParaEmbedding(string livro)
    {
        string caminho = CaminhosDados.Chunks(livro);
        if (!File.Exists(caminho))
        {
            throw new FileNotFoundException($"Ainda não há {livro}.chunks.json — rode o chunking e salve primeiro.");
        }

        return ArquivoChunks.Ler(caminho).Where(c => !string.IsNullOrWhiteSpace(c.Texto)).ToList();
    }

    public async Task<List<float[]>> GerarVetoresAsync(List<string> textos)
    {
        return await ObterGerador().GerarEmLoteAsync(textos);
    }

    public async Task<int> SalvarAsync(List<Chunk> chunks, List<ChunkEmbedding> embeddings)
    {
        await PrepararAsync();
        return await armazenamento.SalvarAsync(chunks, embeddings);
    }

    public async Task<Dictionary<string, int>> ContarVetoresPorLivroAsync()
    {
        await PrepararAsync();
        return await armazenamento.ContarPorLivroAsync();
    }

    public async Task<List<ResultadoBusca>> BuscarAsync(string consulta, int quantidade)
    {
        await PrepararAsync();
        List<float[]> vetor = await ObterGerador().GerarEmLoteAsync(new List<string> { consulta });
        return await armazenamento.BuscarSimilaresAsync(vetor[0], quantidade);
    }

    private GeradorEmbeddingAzureOpenAI ObterGerador()
    {
        if (gerador is null)
        {
            throw new InvalidOperationException(ErroConfiguracao);
        }

        return gerador;
    }

    private async Task PrepararAsync()
    {
        if (!isPreparado)
        {
            await armazenamento.PrepararAsync();
            isPreparado = true;
        }
    }
}
