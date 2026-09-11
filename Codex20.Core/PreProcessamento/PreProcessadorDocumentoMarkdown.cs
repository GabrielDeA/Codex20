using System.Text;
using System.Text.RegularExpressions;

namespace Codex20.Core.PreProcessamento;

/// <summary>
/// Converte o Markdown cru do Azure Document Intelligence numa lista de
/// <see cref="BlocoDocumento"/> limpa e reaproveitável por qualquer livro:
/// <list type="bullet">
///   <item>descobre a página de cada linha a partir de <c>&lt;!-- PageBreak --&gt;</c> e
///         <c>&lt;!-- PageNumber="N" --&gt;</c> (ver <see cref="CalcularPaginaPorLinha"/>)
///         e remove os comentários;</item>
///   <item>remove ruído conhecido do Document Intelligence
///         (<c>&lt;!-- PageHeader="..." --&gt;</c>, <c>&lt;!-- PageFooter="..." --&gt;</c>);</item>
///   <item>descarta cada bloco <c>&lt;figure&gt;...&lt;/figure&gt;</c> inteiro (ilustração,
///         legenda, rótulo de miniatura) — o Markdown revisado não deixa conteúdo de
///         entidade dentro de figura. Também remove tags HTML inline soltas;</item>
///   <item>isola cada <c>&lt;table&gt;...&lt;/table&gt;</c> como <see cref="BlocoTabela"/> atômico
///         (HTML cru, nunca pipe-markdown, nunca dividido);</item>
///   <item>quebra o texto em <see cref="BlocoParagrafo"/> por <b>linha em branco real</b> —
///         nunca por heading Markdown.</item>
/// </list>
/// Nenhuma regra específica de livro mora aqui.
/// </summary>
public class PreProcessadorDocumentoMarkdown
{
    private static readonly Regex RegexComentarioNumeroPagina =
        new(@"<!--\s*PageNumber\s*=\s*""(?<n>\d+)""\s*-->", RegexOptions.IgnoreCase);

    private static readonly Regex RegexComentarioQuebraPagina =
        new(@"<!--\s*PageBreak\s*-->", RegexOptions.IgnoreCase);

    private static readonly Regex RegexComentarioCabecalhoRodape =
        new(@"<!--\s*Page(Header|Footer)\s*=\s*""(?<t>[^""]*)""\s*-->", RegexOptions.IgnoreCase);

    private static readonly Regex RegexQualquerComentario = new(@"<!--.*?-->", RegexOptions.Singleline);

    /// <summary>Tags HTML inline soltas (e <c>&lt;figure&gt;&lt;/figure&gt;</c> numa linha só) que devem sumir mantendo o conteúdo.</summary>
    private static readonly Regex RegexTagInlineSolta =
        new(@"</?(figure|figcaption|i|b|em|strong|sub|sup|u|span|mark|br|small)\s*/?>", RegexOptions.IgnoreCase);

    public List<BlocoDocumento> Processar(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        string[] linhasBrutas = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int?[] paginaPorLinha = CalcularPaginaPorLinha(linhasBrutas);

        var blocos = new List<BlocoDocumento>();
        var paragrafo = new List<string>();
        int? paginaParagrafo = null;

        void DescarregarParagrafo()
        {
            if (paragrafo.Count == 0)
            {
                return;
            }

            blocos.Add(new BlocoParagrafo
            {
                Linhas = new List<string>(paragrafo),
                Pagina = paginaParagrafo,
            });

            paragrafo.Clear();
        }

        for (int i = 0; i < linhasBrutas.Length; i++)
        {
            string linha = linhasBrutas[i];

            // PageHeader/PageFooter: quase sempre são mobília de página ("AÇÕES", "O BRUXO")
            // e são removidos. Exceção: quando o texto tem 3+ palavras ele às vezes carrega o
            // ÚNICO cabeçalho da entidade (ex.: "SAPO GIGANTE Besta Grande, imparcial" no
            // Manual dos Monstros) — nesse caso o conteúdo é preservado como texto.
            linha = RegexComentarioCabecalhoRodape.Replace(linha, m =>
            {
                string t = m.Groups["t"].Value.Trim();
                return t.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 3 ? t : string.Empty;
            });

            // PageNumber, PageBreak e qualquer outro comentário: a página já foi calculada.
            linha = RegexQualquerComentario.Replace(linha, string.Empty);

            string aparado = linha.Trim();

            // Tabela: consome até </table> como bloco atômico.
            if (aparado.StartsWith("<table", StringComparison.OrdinalIgnoreCase))
            {
                DescarregarParagrafo();

                var tabela = new StringBuilder();
                int? paginaTabela = paginaPorLinha[i];
                while (i < linhasBrutas.Length)
                {
                    string linhaTabela = RegexQualquerComentario.Replace(linhasBrutas[i], string.Empty);

                    tabela.Append(linhaTabela.TrimEnd()).Append('\n');
                    if (linhaTabela.Contains("</table>", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                    i++;
                }

                blocos.Add(new BlocoTabela
                {
                    Html = tabela.ToString().Trim(),
                    Pagina = paginaTabela,
                });
                continue;
            }

            // <figure>...</figure>: bloco puramente visual — ilustração, legenda de imagem,
            // rótulo de miniatura, iniciais de revisor. O Markdown já foi revisado para tirar
            // os poucos casos em que o Document Intelligence tinha embrulhado uma ficha de
            // criatura ou um bloco de regras numa figura, então o bloco inteiro é descartado.
            if (aparado.StartsWith("<figure", StringComparison.OrdinalIgnoreCase)
                && !aparado.Contains("</figure>", StringComparison.OrdinalIgnoreCase))
            {
                DescarregarParagrafo();

                while (++i < linhasBrutas.Length)
                {
                    if (linhasBrutas[i].Contains("</figure>", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                }

                continue;
            }

            // Tag de figura solta / outras tags inline: remove mantendo o conteúdo.
            aparado = RegexTagInlineSolta.Replace(aparado, string.Empty).Trim();

            if (aparado.Length == 0)
            {
                DescarregarParagrafo();
                continue;
            }

            if (paragrafo.Count == 0)
            {
                paginaParagrafo = paginaPorLinha[i];
            }

            paragrafo.Add(aparado);
        }

        DescarregarParagrafo();
        return blocos;
    }

    /// <summary>
    /// Página de cada linha do Markdown. O Document Intelligence separa as páginas com
    /// <c>&lt;!-- PageBreak --&gt;</c> e escreve o número impresso (<c>&lt;!-- PageNumber="N" --&gt;</c>)
    /// onde ele aparece na página — nos três livros, quase sempre no rodapé, logo antes do
    /// PageBreak. Por isso o número vale para a página inteira em que está, e não para o que vem
    /// depois dele (ler assim deixava todo o conteúdo uma página atrás).
    /// Página sem número impresso (ilustração, abertura de capítulo) recebe o da anterior + 1 —
    /// conferido nos três livros: entre duas páginas numeradas a contagem nunca pula. As páginas
    /// do começo do livro, antes da primeira numerada, ficam sem página: ali a contagem física não
    /// bate com a impressa (no Manual dos Monstros a 5ª página do PDF é a "4").
    /// </summary>
    private static int?[] CalcularPaginaPorLinha(string[] linhas)
    {
        var paginas = new int?[linhas.Length];
        int? paginaAnterior = null;
        int inicioPagina = 0;

        while (inicioPagina < linhas.Length)
        {
            // A página vai até a linha do PageBreak, inclusive (ou até o fim do arquivo).
            int fimPagina = inicioPagina;
            while (fimPagina < linhas.Length && !RegexComentarioQuebraPagina.IsMatch(linhas[fimPagina]))
            {
                fimPagina++;
            }

            fimPagina = Math.Min(fimPagina + 1, linhas.Length);

            int? pagina = AcharNumeroPagina(linhas, inicioPagina, fimPagina);
            if (pagina is null && paginaAnterior is not null)
            {
                pagina = paginaAnterior + 1;
            }

            for (int i = inicioPagina; i < fimPagina; i++)
            {
                paginas[i] = pagina;
            }

            paginaAnterior = pagina;
            inicioPagina = fimPagina;
        }

        return paginas;
    }

    private static int? AcharNumeroPagina(string[] linhas, int inicio, int fim)
    {
        for (int i = inicio; i < fim; i++)
        {
            Match match = RegexComentarioNumeroPagina.Match(linhas[i]);
            if (match.Success)
            {
                return int.Parse(match.Groups["n"].Value);
            }
        }

        return null;
    }
}
