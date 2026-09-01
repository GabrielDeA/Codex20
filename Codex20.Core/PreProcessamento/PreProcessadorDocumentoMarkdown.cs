using System.Text;
using System.Text.RegularExpressions;

namespace Codex20.Core.PreProcessamento;

/// <summary>
/// Converte o Markdown cru do Azure Document Intelligence numa lista de
/// <see cref="BlocoDocumento"/> limpa e reaproveitável por qualquer livro:
/// <list type="bullet">
///   <item>rastreia o número de página a partir de <c>&lt;!-- PageNumber="N" --&gt;</c>
///         e remove o comentário;</item>
///   <item>remove ruído conhecido do Document Intelligence
///         (<c>&lt;!-- PageBreak --&gt;</c>, <c>&lt;!-- PageHeader="..." --&gt;</c>,
///         <c>&lt;!-- PageFooter="..." --&gt;</c>);</item>
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

        var blocos = new List<BlocoDocumento>();
        var paragrafo = new List<string>();
        int paginaAtual = 0;
        int paginaParagrafo = 0;

        void DescarregarParagrafo()
        {
            if (paragrafo.Count == 0)
            {
                return;
            }

            blocos.Add(new BlocoParagrafo
            {
                Linhas = new List<string>(paragrafo),
                Pagina = paginaParagrafo > 0 ? paginaParagrafo : null,
            });

            paragrafo.Clear();
        }

        for (int i = 0; i < linhasBrutas.Length; i++)
        {
            string linha = linhasBrutas[i];

            // Página corrente: atualiza e remove o comentário.
            Match matchPagina = RegexComentarioNumeroPagina.Match(linha);
            if (matchPagina.Success)
            {
                paginaAtual = int.Parse(matchPagina.Groups["n"].Value);
                linha = RegexComentarioNumeroPagina.Replace(linha, string.Empty);
            }

            linha = RegexComentarioQuebraPagina.Replace(linha, string.Empty);

            // PageHeader/PageFooter: quase sempre são mobília de página ("AÇÕES", "O BRUXO")
            // e são removidos. Exceção: quando o texto tem 3+ palavras ele às vezes carrega o
            // ÚNICO cabeçalho da entidade (ex.: "SAPO GIGANTE Besta Grande, imparcial" no
            // Manual dos Monstros) — nesse caso o conteúdo é preservado como texto.
            linha = RegexComentarioCabecalhoRodape.Replace(linha, m =>
            {
                string t = m.Groups["t"].Value.Trim();
                return t.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 3 ? t : string.Empty;
            });

            linha = RegexQualquerComentario.Replace(linha, string.Empty);

            string aparado = linha.Trim();

            // Tabela: consome até </table> como bloco atômico.
            if (aparado.StartsWith("<table", StringComparison.OrdinalIgnoreCase))
            {
                DescarregarParagrafo();

                var tabela = new StringBuilder();
                int paginaTabela = paginaAtual;
                while (i < linhasBrutas.Length)
                {
                    string linhaTabela = linhasBrutas[i];
                    Match paginaT = RegexComentarioNumeroPagina.Match(linhaTabela);
                    if (paginaT.Success)
                    {
                        paginaAtual = int.Parse(paginaT.Groups["n"].Value);
                        linhaTabela = RegexComentarioNumeroPagina.Replace(linhaTabela, string.Empty);
                    }
                    linhaTabela = RegexQualquerComentario.Replace(linhaTabela, string.Empty);

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
                    Pagina = paginaTabela > 0 ? paginaTabela : null,
                });
                continue;
            }

            // <figure>...</figure>: bloco puramente visual — ilustração, legenda de imagem,
            // rótulo de miniatura, iniciais de revisor. O Markdown já foi revisado para tirar
            // os poucos casos em que o Document Intelligence tinha embrulhado uma ficha de
            // criatura ou um bloco de regras numa figura, então o bloco inteiro é descartado.
            // Só o número de página que aparece lá dentro é aproveitado.
            if (aparado.StartsWith("<figure", StringComparison.OrdinalIgnoreCase)
                && !aparado.Contains("</figure>", StringComparison.OrdinalIgnoreCase))
            {
                DescarregarParagrafo();

                while (++i < linhasBrutas.Length)
                {
                    Match paginaFig = RegexComentarioNumeroPagina.Match(linhasBrutas[i]);
                    if (paginaFig.Success)
                    {
                        paginaAtual = int.Parse(paginaFig.Groups["n"].Value);
                    }

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
                paginaParagrafo = paginaAtual;
            }

            paragrafo.Add(aparado);
        }

        DescarregarParagrafo();
        return blocos;
    }
}
