using System.Text;
using Codex20.Core.Busca;
using Codex20.Core.Chunking;
using Codex20.Core.Embeddings;
using Microsoft.Data.Sqlite;

namespace Codex20.Embeddings;

/// <summary>
/// Índice léxico no FTS5, o módulo de busca textual que já vem compilado no e_sqlite3. É outra
/// virtual table no mesmo arquivo do vector store, e ranqueia por BM25.
/// </summary>
public class IndiceTextualFts5 : IIndiceTextual
{
    private readonly string stringConexao;
    private readonly string tabela;

    /// <param name="tabela">
    /// Vai interpolada no SQL (o SQLite não parametriza nome de tabela): só literal do código,
    /// nunca configuração ou entrada de usuário.
    /// </param>
    public IndiceTextualFts5(string caminhoBanco, string tabela)
    {
        stringConexao = ConexaoBanco.MontarStringConexao(caminhoBanco);
        this.tabela = tabela;
    }

    public async Task PrepararAsync()
    {
        using SqliteConnection conexao = await ConexaoBanco.AbrirAsync(stringConexao);
        using SqliteCommand comando = conexao.CreateCommand();

        // Tabela standalone, com cópia do texto: external content sobre a vec0 exigiria triggers,
        // e o SQLite não aceita trigger em virtual table.
        // Só o texto é indexado: o nome da entidade já abre o próprio texto do chunk ("## SOLAR"),
        // e indexá-lo de novo contaria o mesmo termo duas vezes.
        // Sem porter: é stemmer de inglês e só atrapalharia em português. remove_diacritics 2 (e
        // não 1, que erra com caracteres de mais de um diacrítico) faz "acao" achar "ação".
        comando.CommandText = $"""
            CREATE VIRTUAL TABLE IF NOT EXISTS {tabela} USING fts5(
                texto,
                chunk_id UNINDEXED,
                nome_entidade UNINDEXED,
                livro UNINDEXED,
                pagina_inicio UNINDEXED,
                pagina_fim UNINDEXED,
                is_fallback UNINDEXED,
                tokenize = 'unicode61 remove_diacritics 2'
            )
            """;
        await comando.ExecuteNonQueryAsync();
    }

    public async Task<int> IndexarAsync(List<Chunk> chunks)
    {
        using SqliteConnection conexao = await ConexaoBanco.AbrirAsync(stringConexao);
        using (SqliteTransaction transacao = conexao.BeginTransaction())
        {
            foreach (string livro in ListarLivros(chunks))
            {
                using SqliteCommand apagar = conexao.CreateCommand();
                apagar.Transaction = transacao;
                apagar.CommandText = $"DELETE FROM {tabela} WHERE livro = @livro";
                apagar.Parameters.AddWithValue("@livro", livro);
                await apagar.ExecuteNonQueryAsync();
            }

            var gravados = new HashSet<Guid>();

            foreach (Chunk chunk in chunks)
            {
                // Mesma regra do vector store: texto repetido dentro do livro entra uma vez só, senão
                // as contagens dos dois índices divergem e o BM25 devolve cartões duplicados.
                if (!gravados.Add(chunk.Id))
                {
                    continue;
                }

                // Mesma normalização do vector store (nulo vira "" e 0): o mesmo chunk voltando pelas
                // duas pernas do RRF tem que ter a mesma cara.
                using SqliteCommand inserir = conexao.CreateCommand();
                inserir.Transaction = transacao;
                inserir.CommandText = $"""
                    INSERT INTO {tabela}(texto, chunk_id, nome_entidade, livro, pagina_inicio, pagina_fim, is_fallback)
                    VALUES (@texto, @chunkId, @nomeEntidade, @livro, @paginaInicio, @paginaFim, @isFallback)
                    """;
                inserir.Parameters.AddWithValue("@texto", chunk.Texto);
                inserir.Parameters.AddWithValue("@chunkId", chunk.Id.ToString());
                inserir.Parameters.AddWithValue("@nomeEntidade", chunk.NomeEntidade ?? string.Empty);
                inserir.Parameters.AddWithValue("@livro", chunk.Livro);
                inserir.Parameters.AddWithValue("@paginaInicio", chunk.PaginaInicio ?? 0);
                inserir.Parameters.AddWithValue("@paginaFim", chunk.PaginaFim ?? 0);
                inserir.Parameters.AddWithValue("@isFallback", chunk.IsFallback ? 1 : 0);
                await inserir.ExecuteNonQueryAsync();
            }

            transacao.Commit();

            // Junta os segmentos do índice invertido: uma carga em massa deixa dezenas deles, e toda
            // consulta pagaria a leitura de todos.
            using SqliteCommand otimizar = conexao.CreateCommand();
            otimizar.CommandText = $"INSERT INTO {tabela}({tabela}) VALUES('optimize')";
            await otimizar.ExecuteNonQueryAsync();

            return gravados.Count;
        }
    }

    public async Task<List<ResultadoBusca>> BuscarTextoAsync(string consulta, int quantidade)
    {
        var resultados = new List<ResultadoBusca>();

        string expressao = MontarExpressao(consulta);
        if (expressao.Length == 0)
        {
            return resultados;
        }

        using SqliteConnection conexao = await ConexaoBanco.AbrirAsync(stringConexao);
        using SqliteCommand comando = conexao.CreateCommand();

        // rank é o bm25() com pesos default, e ordenar por ele ativa o top-k interno do FTS5. O
        // bm25() do SQLite é negativo (mais negativo = melhor), por isso a ordem é crescente.
        comando.CommandText = $"""
            SELECT texto, nome_entidade, livro, pagina_inicio, pagina_fim, is_fallback, bm25({tabela})
            FROM {tabela}
            WHERE {tabela} MATCH @consulta
            ORDER BY rank
            LIMIT @k
            """;
        comando.Parameters.AddWithValue("@consulta", expressao);
        comando.Parameters.AddWithValue("@k", quantidade);

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
                // Invertido para valer "maior = melhor", como nas outras estratégias.
                Pontuacao = -leitor.GetDouble(6),
            });
        }

        return resultados;
    }

    public async Task<Dictionary<string, int>> ContarPorLivroAsync()
    {
        using SqliteConnection conexao = await ConexaoBanco.AbrirAsync(stringConexao);
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

    /// <summary>
    /// Transforma a entrada livre numa expressão que o parser do FTS5 aceita. Só sobrevivem as
    /// sequências de letras e dígitos, cada uma entre aspas — assim '-', '*', ':', parênteses e
    /// palavras como AND/NEAR entram como texto, não como operador. Devolve "" se não sobrar termo.
    /// </summary>
    private static string MontarExpressao(string consulta)
    {
        var termos = new List<string>();
        var atual = new StringBuilder();

        foreach (char c in consulta)
        {
            if (char.IsLetterOrDigit(c))
            {
                atual.Append(c);
                continue;
            }

            if (atual.Length > 0)
            {
                termos.Add(atual.ToString());
                atual.Clear();
            }
        }

        if (atual.Length > 0)
        {
            termos.Add(atual.ToString());
        }

        // OR, e não o AND implícito do FTS5: com AND, "pontos de vida do aarakocra" só acharia
        // chunks com as cinco palavras. É o próprio BM25, pelo peso dos termos raros, que deve
        // decidir o ranking — trocar para AND esvazia a estratégia.
        return string.Join(" OR ", termos.Select(termo => $"\"{termo}\""));
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
}
