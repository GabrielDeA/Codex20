using Codex20.Core.Chunking;
using Codex20.Core.Embeddings;
using Microsoft.Data.Sqlite;

namespace Codex20.Embeddings;

/// <summary>
/// Vector store em SQLite com a extensão sqlite-vec. O banco é um arquivo só e a coleção é uma
/// virtual table <c>vec0</c>: a coluna do vetor é indexada e as colunas com <c>+</c> são
/// carga (o texto do chunk e seus metadados) que volta junto na busca.
/// </summary>
public class ArmazenamentoVetorialSqlite : IArmazenamentoVetorial
{
    /// <summary>
    /// Dimensão do vetor. Precisa bater com o modelo do deployment: 1536 para
    /// text-embedding-3-small e ada-002, 3072 para text-embedding-3-large.
    /// </summary>
    public const int Dimensoes = 3072;

    private readonly string stringConexao;
    private readonly string tabela;

    public ArmazenamentoVetorialSqlite(string caminhoBanco, string tabela)
    {
        stringConexao = ConexaoBanco.MontarStringConexao(caminhoBanco);
        this.tabela = tabela;
    }

    private const string Colunas = "id, vetor, texto, nome_entidade, livro, pagina_inicio, pagina_fim, is_fallback, modelo";

    public async Task PrepararAsync()
    {
        using SqliteConnection conexao = await AbrirAsync();
        await ExecutarAsync(conexao, null, SqlCriarTabela(tabela));
    }

    public async Task<int> SalvarAsync(List<Chunk> chunks, List<ChunkEmbedding> embeddings)
    {
        int gravados = await GravarAsync(chunks, embeddings);
        await CompactarAsync();
        return gravados;
    }

    /// <summary>
    /// Reescreve a vec0 sem espaço morto. Ela guarda os vetores em blocos de 1024 posições, e o
    /// DELETE só marca posições como livres: as inserções seguintes vão para blocos novos no fim.
    /// Cada regravação de livro deixava para trás um bloco de ~12 MB, e o banco chegou a ter 3 de 7
    /// blocos vazios. VACUUM sozinho não resolve, porque para o SQLite essas páginas ainda são da
    /// tabela — é preciso copiar os vetores para uma vec0 nova.
    /// </summary>
    public async Task CompactarAsync()
    {
        string temporaria = tabela + "_compactando";

        using (SqliteConnection conexao = await AbrirAsync())
        {
            // Tudo numa transação: se algo falhar no meio, a tabela original fica intacta. Copia duas
            // vezes em vez de renomear porque o sqlite-vec 0.1.7 não documenta RENAME na vec0.
            using SqliteTransaction transacao = conexao.BeginTransaction();
            await ExecutarAsync(conexao, transacao, $"DROP TABLE IF EXISTS {temporaria}");
            await ExecutarAsync(conexao, transacao, SqlCriarTabela(temporaria));
            await ExecutarAsync(conexao, transacao, $"INSERT INTO {temporaria}({Colunas}) SELECT {Colunas} FROM {tabela}");
            await ExecutarAsync(conexao, transacao, $"DROP TABLE {tabela}");
            await ExecutarAsync(conexao, transacao, SqlCriarTabela(tabela));
            await ExecutarAsync(conexao, transacao, $"INSERT INTO {tabela}({Colunas}) SELECT {Colunas} FROM {temporaria}");
            await ExecutarAsync(conexao, transacao, $"DROP TABLE {temporaria}");
            transacao.Commit();
        }

        // Só agora as páginas dos blocos antigos estão livres; VACUUM as devolve ao disco e não roda
        // dentro de transação.
        using SqliteConnection conexaoVacuum = await AbrirAsync();
        await ExecutarAsync(conexaoVacuum, null, "VACUUM");
    }

    private string SqlCriarTabela(string nome)
    {
        return $"""
            CREATE VIRTUAL TABLE IF NOT EXISTS {nome} USING vec0(
                id TEXT PRIMARY KEY,
                vetor FLOAT[{Dimensoes}] distance_metric=cosine,
                +texto TEXT,
                +nome_entidade TEXT,
                +livro TEXT,
                +pagina_inicio INTEGER,
                +pagina_fim INTEGER,
                +is_fallback INTEGER,
                +modelo TEXT
            )
            """;
    }

    private static async Task ExecutarAsync(SqliteConnection conexao, SqliteTransaction? transacao, string sql)
    {
        using SqliteCommand comando = conexao.CreateCommand();
        comando.Transaction = transacao;
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync();
    }

    private async Task<int> GravarAsync(List<Chunk> chunks, List<ChunkEmbedding> embeddings)
    {
        var chunkPorId = new Dictionary<Guid, Chunk>();
        foreach (Chunk chunk in chunks)
        {
            chunkPorId[chunk.Id] = chunk;
        }

        using SqliteConnection conexao = await AbrirAsync();
        using SqliteTransaction transacao = conexao.BeginTransaction();

        // Gravar um livro substitui tudo o que havia dele. Sem isso, um chunk cujo texto mudou
        // entraria com Id novo e a versão antiga ficaria para trás, competindo na busca.
        foreach (string livro in ListarLivros(chunks))
        {
            using SqliteCommand apagar = conexao.CreateCommand();
            apagar.Transaction = transacao;
            apagar.CommandText = $"DELETE FROM {tabela} WHERE livro = @livro";
            apagar.Parameters.AddWithValue("@livro", livro);
            await apagar.ExecuteNonQueryAsync();
        }

        var gravados = new HashSet<Guid>();

        foreach (ChunkEmbedding embedding in embeddings)
        {
            if (!chunkPorId.TryGetValue(embedding.ChunkId, out Chunk? chunk))
            {
                throw new InvalidOperationException($"Embedding sem chunk correspondente: {embedding.ChunkId}");
            }

            // Texto repetido dentro do livro gera o mesmo Id (ex. ações lendárias iguais entre
            // variantes de dragão): grava uma vez só, senão viola a chave e polui a busca.
            if (!gravados.Add(chunk.Id))
            {
                continue;
            }

            using SqliteCommand inserir = conexao.CreateCommand();
            inserir.Transaction = transacao;
            inserir.CommandText = $"""
                INSERT INTO {tabela}(id, vetor, texto, nome_entidade, livro, pagina_inicio, pagina_fim, is_fallback, modelo)
                VALUES (@id, @vetor, @texto, @nomeEntidade, @livro, @paginaInicio, @paginaFim, @isFallback, @modelo)
                """;
            inserir.Parameters.AddWithValue("@id", chunk.Id.ToString());
            inserir.Parameters.AddWithValue("@vetor", ParaBytes(embedding.Vetor));
            inserir.Parameters.AddWithValue("@texto", chunk.Texto);
            inserir.Parameters.AddWithValue("@nomeEntidade", chunk.NomeEntidade ?? string.Empty);
            inserir.Parameters.AddWithValue("@livro", chunk.Livro);
            inserir.Parameters.AddWithValue("@paginaInicio", chunk.PaginaInicio ?? 0);
            inserir.Parameters.AddWithValue("@paginaFim", chunk.PaginaFim ?? 0);
            inserir.Parameters.AddWithValue("@isFallback", chunk.IsFallback ? 1 : 0);
            inserir.Parameters.AddWithValue("@modelo", embedding.Modelo);
            await inserir.ExecuteNonQueryAsync();
        }

        transacao.Commit();
        return gravados.Count;
    }

    private static List<string> ListarLivros(List<Chunk> chunks)
    {
        var livros = new List<string>();
        foreach (Chunk chunk in chunks)
        {
            if (!livros.Contains(chunk.Livro))
            {
                livros.Add(chunk.Livro);
            }
        }

        return livros;
    }

    public async Task<List<ResultadoBusca>> BuscarSimilaresAsync(float[] vetorConsulta, int quantidade)
    {
        using SqliteConnection conexao = await AbrirAsync();
        using SqliteCommand comando = conexao.CreateCommand();
        comando.CommandText = $"""
            SELECT texto, nome_entidade, livro, pagina_inicio, pagina_fim, is_fallback, distance
            FROM {tabela}
            WHERE vetor MATCH @consulta AND k = @k
            ORDER BY distance
            """;
        comando.Parameters.AddWithValue("@consulta", ParaBytes(vetorConsulta));
        comando.Parameters.AddWithValue("@k", quantidade);

        var resultados = new List<ResultadoBusca>();
        using SqliteDataReader leitor = await comando.ExecuteReaderAsync();

        while (await leitor.ReadAsync())
        {
            string nomeEntidade = leitor.GetString(1);
            int paginaInicio = leitor.GetInt32(3);
            int paginaFim = leitor.GetInt32(4);

            resultados.Add(new ResultadoBusca
            {
                Chunk = new Chunk
                {
                    Texto = leitor.GetString(0),
                    NomeEntidade = string.IsNullOrEmpty(nomeEntidade) ? null : nomeEntidade,
                    Livro = leitor.GetString(2),
                    PaginaInicio = paginaInicio == 0 ? null : paginaInicio,
                    PaginaFim = paginaFim == 0 ? null : paginaFim,
                    IsFallback = leitor.GetInt32(5) == 1,
                },
                // distance_metric=cosine devolve distância; similaridade é o complemento.
                Pontuacao = 1 - leitor.GetDouble(6),
            });
        }

        return resultados;
    }

    /// <summary>Quantos vetores cada livro tem no banco.</summary>
    public async Task<Dictionary<string, int>> ContarPorLivroAsync()
    {
        using SqliteConnection conexao = await AbrirAsync();
        using SqliteCommand comando = conexao.CreateCommand();
        comando.CommandText = $"SELECT livro, COUNT(*) FROM {tabela} GROUP BY livro";

        var contagem = new Dictionary<string, int>();
        using SqliteDataReader leitor = await comando.ExecuteReaderAsync();

        while (await leitor.ReadAsync())
        {
            contagem[leitor.GetString(0)] = leitor.GetInt32(1);
        }

        return contagem;
    }

    private Task<SqliteConnection> AbrirAsync()
    {
        return ConexaoBanco.AbrirAsync(stringConexao);
    }

    private static byte[] ParaBytes(float[] vetor)
    {
        if (vetor.Length != Dimensoes)
        {
            throw new ArgumentException($"Vetor com {vetor.Length} dimensões; a tabela espera {Dimensoes}.", nameof(vetor));
        }

        var bytes = new byte[vetor.Length * sizeof(float)];
        Buffer.BlockCopy(vetor, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
