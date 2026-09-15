using Codex20.Core.Busca;
using Codex20.Core.Chunking;
using Codex20.Core.Embeddings;
using Codex20.Embeddings;

namespace Codex20.Painel.Servicos;

/// <summary>
/// Acesso ao gerador de embeddings, ao banco vetorial e ao índice BM25. Sem credenciais do Azure
/// OpenAI o Painel continua abrindo: só o que chama a API fica bloqueado, com a mensagem de
/// <see cref="ErroConfiguracao"/>.
/// </summary>
public class ServicoEmbeddings
{
    private readonly IGeradorEmbedding? gerador;
    private readonly IArmazenamentoVetorial armazenamento;
    private readonly IIndiceTextual indiceTextual;
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
        // Mesmo arquivo: o índice léxico é outra virtual table ao lado da vec0.
        indiceTextual = new IndiceTextualFts5(CaminhosDados.Banco(), "chunks_fts");
    }

    public string? ErroConfiguracao { get; }

    public bool IsConfigurado => gerador is not null;

    public string Modelo => gerador is null ? "(não configurado)" : gerador.Modelo;

    /// <summary>Nulo sem credenciais. Exposto para o <see cref="ServicoBusca"/> montar as estratégias.</summary>
    public IGeradorEmbedding? Gerador => gerador;

    public IArmazenamentoVetorial Armazenamento => armazenamento;

    public IIndiceTextual IndiceTextual => indiceTextual;

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

    /// <summary>Monta o índice BM25 do livro a partir do mesmo <c>.chunks.json</c>. Não chama a API.</summary>
    public async Task<int> IndexarTextoAsync(string livro)
    {
        await PrepararAsync();
        return await indiceTextual.IndexarAsync(LerChunksParaEmbedding(livro));
    }

    public async Task<Dictionary<string, int>> ContarIndiceTextualPorLivroAsync()
    {
        await PrepararAsync();
        return await indiceTextual.ContarPorLivroAsync();
    }

    public async Task PrepararAsync()
    {
        // Dois circuitos podem entrar aqui juntos; tudo bem enquanto for só CREATE ... IF NOT EXISTS.
        if (!isPreparado)
        {
            await armazenamento.PrepararAsync();
            await indiceTextual.PrepararAsync();
            isPreparado = true;
        }
    }

    private IGeradorEmbedding ObterGerador()
    {
        if (gerador is null)
        {
            throw new InvalidOperationException(ErroConfiguracao);
        }

        return gerador;
    }
}
