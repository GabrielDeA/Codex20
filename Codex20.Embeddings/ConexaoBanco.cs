using Microsoft.Data.Sqlite;

namespace Codex20.Embeddings;

/// <summary>
/// Conexões ao arquivo que guarda, lado a lado, a vec0 dos vetores e o índice FTS5 do BM25.
/// </summary>
public static class ConexaoBanco
{
    public static string MontarStringConexao(string caminhoBanco)
    {
        string? diretorio = Path.GetDirectoryName(caminhoBanco);
        if (!string.IsNullOrEmpty(diretorio))
        {
            Directory.CreateDirectory(diretorio);
        }

        // Indexar o BM25 e gravar vetores ao mesmo tempo disputariam o arquivo: esperar alguns
        // segundos pelo lock é melhor que falhar na hora com SQLITE_BUSY.
        return $"Data Source={caminhoBanco};Default Timeout=10";
    }

    /// <summary>
    /// Abre carregando o sqlite-vec — inclusive para quem só mexe no FTS5. O arquivo tem uma vec0,
    /// e o que fizer o SQLite instanciá-la (reload de schema ao criar outra virtual table, VACUUM,
    /// integrity_check) falha com "no such module: vec0" sem a extensão, num ponto que não parece
    /// ter nada a ver com vetores. Não remova achando que é desperdício.
    /// </summary>
    public static async Task<SqliteConnection> AbrirAsync(string stringConexao)
    {
        var conexao = new SqliteConnection(stringConexao);
        await conexao.OpenAsync();
        conexao.LoadVector();
        return conexao;
    }
}
