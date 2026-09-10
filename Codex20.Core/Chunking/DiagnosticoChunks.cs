using System.Text.RegularExpressions;

namespace Codex20.Core.Chunking;

/// <summary>
/// Heurísticas de validação do chunking entity-aware: apontam o que precisa ser conferido à mão
/// no Markdown bruto. Não alteram chunk nenhum. Usadas pelo relatório do Chunking.Playground e
/// pelo Painel — ficam aqui para os dois contarem igual.
/// </summary>
public static class DiagnosticoChunks
{
    private static readonly Regex RegexFormatoNomeLimpo = new(@"^[\p{Lu}0-9][\p{Lu}0-9 ,'/()+\-\.À-ſ]{1,58}$");

    private static readonly Regex RegexTerminaLimpo = new(@"[\.\!\?:;""'’)\]]\s*$|\bAÇÕES\s*$", RegexOptions.IgnoreCase);

    private static readonly Regex RegexAbreTabela = new("<table", RegexOptions.IgnoreCase);

    private static readonly Regex RegexFechaTabela = new("</table>", RegexOptions.IgnoreCase);

    public static bool IsNomeLimpo(string nome)
    {
        string n = nome.Trim();
        return n.Length >= 2 && n.Length <= 60
               && RegexFormatoNomeLimpo.IsMatch(n.ToUpperInvariant())
               && !n.Contains("  ");
    }

    /// <summary>Chunk que é uma tabela sozinha (começa em <c>&lt;table</c>).</summary>
    public static bool IsTabela(Chunk chunk)
    {
        return chunk.Texto.TrimStart().StartsWith("<table", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsTabelaCortada(Chunk chunk)
    {
        return RegexAbreTabela.Matches(chunk.Texto).Count != RegexFechaTabela.Matches(chunk.Texto).Count;
    }

    /// <summary>
    /// <c>true</c> quando dois chunks de entidade vizinhos têm uma fronteira duvidosa: o
    /// <paramref name="atual"/> termina em frase incompleta e o <paramref name="proximo"/> já é
    /// uma entidade com nome — sinal de que o corte roubou texto de um para o outro.
    /// </summary>
    public static bool IsFronteiraSuspeita(Chunk atual, Chunk proximo)
    {
        if (atual.IsFallback || proximo.IsFallback || proximo.NomeEntidade is null)
        {
            return false;
        }

        string fimAparado = atual.Texto.TrimEnd();
        string ultimaLinha = fimAparado[(fimAparado.LastIndexOf('\n') + 1)..].TrimStart();
        bool terminaLimpo = RegexTerminaLimpo.IsMatch(fimAparado)
                         || fimAparado.EndsWith('m')            // "alcance 1,5 m"
                         || fimAparado.EndsWith('>')            // fim de tabela
                         || ultimaLinha.StartsWith('·') || ultimaLinha.StartsWith('-')  // item de lista
                         || ultimaLinha.StartsWith('<');
        return !terminaLimpo;
    }
}
