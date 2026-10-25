using System.Text.RegularExpressions;
using Codex20.Avaliacao.Gabarito;

namespace Codex20.Avaliacao;

/// <summary>
/// Acha "Livro do Jogador, p. 95" na resposta sem RAG. Pedimos "(Livro do Jogador, p. 95)", mas
/// aceita também "pág.", "págs.", "página", intervalo "p. 95-96" e várias páginas na mesma citação
/// ("p. 95, 97"). Citação só com o livro, sem página, não entra: não há o que comparar com o gabarito.
/// </summary>
public static class LeitorFontesCitadas
{
    private static readonly Regex RegexFonte = new(
        @"(Livro do Jogador|Guia do Mestre|Manual dos Monstros)\s*[,:\-–—]?\s*(?:pp?\.|p[áa]gs?\.|p[áa]ginas?)\s*(\d+(?:\s*[-–]\s*\d+)?(?:\s*(?:,|e)\s*\d+(?:\s*[-–]\s*\d+)?)*)",
        RegexOptions.IgnoreCase);

    private static readonly Regex RegexIntervalo = new(@"(\d+)(?:\s*[-–]\s*(\d+))?");

    private static readonly Regex RegexParenteses = new(@"\(([^()]*)\)");

    private static readonly Regex RegexLivro = new(@"Livro do Jogador|Guia do Mestre|Manual dos Monstros", RegexOptions.IgnoreCase);

    /// <summary>
    /// Livros citados entre parênteses, com ou sem página: "(Livro do Jogador)". Só dentro de
    /// parênteses porque a recusa também nomeia os livros ("não aparece no Guia do Mestre"), e isso
    /// não é citação.
    /// </summary>
    public static List<string> ExtrairLivros(string texto)
    {
        var livros = new List<string>();
        foreach (Match parenteses in RegexParenteses.Matches(texto))
        {
            foreach (Match livro in RegexLivro.Matches(parenteses.Groups[1].Value))
            {
                string apelido = Apelido(livro.Value);
                if (!livros.Contains(apelido))
                {
                    livros.Add(apelido);
                }
            }
        }

        return livros;
    }

    public static List<IntervaloPaginas> Extrair(string texto)
    {
        var paginas = new List<IntervaloPaginas>();

        foreach (Match fonte in RegexFonte.Matches(texto))
        {
            string livro = Apelido(fonte.Groups[1].Value);
            foreach (Match intervalo in RegexIntervalo.Matches(fonte.Groups[2].Value))
            {
                int inicio = int.Parse(intervalo.Groups[1].Value);
                int fim = intervalo.Groups[2].Success ? int.Parse(intervalo.Groups[2].Value) : inicio;
                var novo = new IntervaloPaginas { Livro = livro, Inicio = inicio, Fim = Math.Max(inicio, fim) };

                if (!IsRepetido(paginas, novo))
                {
                    paginas.Add(novo);
                }
            }
        }

        return paginas;
    }

    private static bool IsRepetido(List<IntervaloPaginas> paginas, IntervaloPaginas novo)
    {
        foreach (IntervaloPaginas existente in paginas)
        {
            if (existente.Livro == novo.Livro && existente.Inicio == novo.Inicio && existente.Fim == novo.Fim)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>O inverso do <c>NomesLivros.Exibicao</c>.</summary>
    private static string Apelido(string nome)
    {
        string normalizado = nome.ToLowerInvariant();
        if (normalizado.Contains("jogador"))
        {
            return "jogador";
        }

        if (normalizado.Contains("mestre"))
        {
            return "mestre";
        }

        return "monstro";
    }
}
