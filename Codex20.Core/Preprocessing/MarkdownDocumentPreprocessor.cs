using System.Text;
using System.Text.RegularExpressions;

namespace Codex20.Core.Preprocessing;

/// <summary>
/// Converte o Markdown cru do Azure Document Intelligence numa lista de
/// <see cref="DocumentBlock"/> limpa e reaproveitável por qualquer livro:
/// <list type="bullet">
///   <item>rastreia o número de página a partir de <c>&lt;!-- PageNumber="N" --&gt;</c>
///         e remove o comentário;</item>
///   <item>remove ruído conhecido do Document Intelligence
///         (<c>&lt;!-- PageBreak --&gt;</c>, <c>&lt;!-- PageHeader="..." --&gt;</c>,
///         <c>&lt;!-- PageFooter="..." --&gt;</c>);</item>
///   <item>descarta cada bloco <c>&lt;figure&gt;...&lt;/figure&gt;</c> inteiro (ilustração,
///         legenda, rótulo de miniatura) — o Markdown revisado não deixa conteúdo de
///         entidade dentro de figura. Também remove tags HTML inline soltas;</item>
///   <item>isola cada <c>&lt;table&gt;...&lt;/table&gt;</c> como <see cref="TableBlock"/> atômico
///         (HTML cru, nunca pipe-markdown, nunca dividido);</item>
///   <item>quebra o texto em <see cref="ParagraphBlock"/> por <b>linha em branco real</b> —
///         nunca por heading Markdown.</item>
/// </list>
/// Nenhuma regra específica de livro mora aqui.
/// </summary>
public class MarkdownDocumentPreprocessor
{
    private static readonly Regex PageNumberComment =
        new(@"<!--\s*PageNumber\s*=\s*""(?<n>\d+)""\s*-->", RegexOptions.IgnoreCase);

    private static readonly Regex PageBreakComment =
        new(@"<!--\s*PageBreak\s*-->", RegexOptions.IgnoreCase);

    private static readonly Regex PageHeaderComment =
        new(@"<!--\s*Page(Header|Footer)\s*=\s*""(?<t>[^""]*)""\s*-->", RegexOptions.IgnoreCase);

    private static readonly Regex AnyComment = new(@"<!--.*?-->", RegexOptions.Singleline);

    /// <summary>Tags HTML inline soltas (e <c>&lt;figure&gt;&lt;/figure&gt;</c> numa linha só) que devem sumir mantendo o conteúdo.</summary>
    private static readonly Regex StrayInlineTag =
        new(@"</?(figure|figcaption|i|b|em|strong|sub|sup|u|span|mark|br|small)\s*/?>", RegexOptions.IgnoreCase);

    public List<DocumentBlock> Process(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        string[] rawLines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        var blocks = new List<DocumentBlock>();
        var paragraph = new List<string>();
        int currentPage = 0;
        int paragraphPage = 0;

        void FlushParagraph()
        {
            if (paragraph.Count == 0)
            {
                return;
            }

            blocks.Add(new ParagraphBlock
            {
                Lines = new List<string>(paragraph),
                Page = paragraphPage > 0 ? paragraphPage : null,
            });

            paragraph.Clear();
        }

        for (int i = 0; i < rawLines.Length; i++)
        {
            string line = rawLines[i];

            // Página corrente: atualiza e remove o comentário.
            Match pageMatch = PageNumberComment.Match(line);
            if (pageMatch.Success)
            {
                currentPage = int.Parse(pageMatch.Groups["n"].Value);
                line = PageNumberComment.Replace(line, string.Empty);
            }

            line = PageBreakComment.Replace(line, string.Empty);

            // PageHeader/PageFooter: quase sempre são mobília de página ("AÇÕES", "O BRUXO")
            // e são removidos. Exceção: quando o texto tem 3+ palavras ele às vezes carrega o
            // ÚNICO cabeçalho da entidade (ex.: "SAPO GIGANTE Besta Grande, imparcial" no
            // Manual dos Monstros) — nesse caso o conteúdo é preservado como texto.
            line = PageHeaderComment.Replace(line, m =>
            {
                string t = m.Groups["t"].Value.Trim();
                return t.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 3 ? t : string.Empty;
            });

            line = AnyComment.Replace(line, string.Empty);

            string trimmed = line.Trim();

            // Tabela: consome até </table> como bloco atômico.
            if (trimmed.StartsWith("<table", StringComparison.OrdinalIgnoreCase))
            {
                FlushParagraph();

                var table = new StringBuilder();
                int tablePage = currentPage;
                while (i < rawLines.Length)
                {
                    string tLine = rawLines[i];
                    Match tPage = PageNumberComment.Match(tLine);
                    if (tPage.Success)
                    {
                        currentPage = int.Parse(tPage.Groups["n"].Value);
                        tLine = PageNumberComment.Replace(tLine, string.Empty);
                    }
                    tLine = AnyComment.Replace(tLine, string.Empty);

                    table.Append(tLine.TrimEnd()).Append('\n');
                    if (tLine.Contains("</table>", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                    i++;
                }

                blocks.Add(new TableBlock
                {
                    Html = table.ToString().Trim(),
                    Page = tablePage > 0 ? tablePage : null,
                });
                continue;
            }

            // <figure>...</figure>: bloco puramente visual — ilustração, legenda de imagem,
            // rótulo de miniatura, iniciais de revisor. O Markdown já foi revisado para tirar
            // os poucos casos em que o Document Intelligence tinha embrulhado uma ficha de
            // criatura ou um bloco de regras numa figura, então o bloco inteiro é descartado.
            // Só o número de página que aparece lá dentro é aproveitado.
            if (trimmed.StartsWith("<figure", StringComparison.OrdinalIgnoreCase)
                && !trimmed.Contains("</figure>", StringComparison.OrdinalIgnoreCase))
            {
                FlushParagraph();

                while (++i < rawLines.Length)
                {
                    Match figPage = PageNumberComment.Match(rawLines[i]);
                    if (figPage.Success)
                    {
                        currentPage = int.Parse(figPage.Groups["n"].Value);
                    }

                    if (rawLines[i].Contains("</figure>", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                }

                continue;
            }

            // Tag de figura solta / outras tags inline: remove mantendo o conteúdo.
            trimmed = StrayInlineTag.Replace(trimmed, string.Empty).Trim();

            if (trimmed.Length == 0)
            {
                FlushParagraph();
                continue;
            }

            if (paragraph.Count == 0)
            {
                paragraphPage = currentPage;
            }

            paragraph.Add(trimmed);
        }

        FlushParagraph();
        return blocks;
    }
}
