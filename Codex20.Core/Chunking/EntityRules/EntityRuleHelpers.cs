using System.Globalization;
using System.Text.RegularExpressions;
using Codex20.Core.Preprocessing;

namespace Codex20.Core.Chunking.EntityRules;

/// <summary>Utilitários compartilhados pelas regras de entidade dos três livros.</summary>
internal static class EntityRuleHelpers
{
    private static readonly Regex HeadingPrefix = new(@"^#{1,6}\s+");

    private static readonly Regex AnyHeading = new(@"^#{1,6}\s+\S");

    private static readonly Regex Whitespace = new(@"\s+");

    /// <summary>Conectivos que podem aparecer em minúsculas no meio de um nome em CAIXA ALTA.</summary>
    private static readonly HashSet<string> Connectives =
        new(StringComparer.OrdinalIgnoreCase) { "de", "do", "da", "dos", "das", "e", "ou", "a", "o", "the", "of" };

    /// <summary>
    /// Sub-cabeçalhos do bloco de atributos de uma criatura que o Document Intelligence às
    /// vezes marca como heading Markdown (<c># AÇÕES</c>, <c>## REAÇÕES</c>, ...). Não são
    /// fronteira de entidade — pertencem à ficha da criatura corrente.
    /// </summary>
    private static readonly string[] StatBlockSubheadings =
    {
        "AÇÕES", "AÇOES", "REAÇÕES", "REAÇOES",
        "AÇÕES LENDÁRIAS", "AÇÕES LENDARIAS", "AÇÕES DE COVIL",
        "AÇÕES DE COVIL E EFEITOS REGIONAIS", "EFEITOS REGIONAIS",
        "AÇÕES ADICIONAIS", "AÇÕES BÔNUS",
    };

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string StripHeading(string line) => HeadingPrefix.Replace(line, string.Empty).Trim();

    public static bool IsHeadingLine(string line) => AnyHeading.IsMatch(line);

    /// <summary>
    /// Heading que de fato inicia outra criatura/grupo. Exclui:
    /// sub-cabeçalhos de ficha (<c># AÇÕES</c>); traços de criatura que o Document
    /// Intelligence marcou como heading por engano (<c>### Nome. Texto longo...</c>);
    /// e sidebars <c>VARIAÇÃO:</c> / <c>OPCIONAL:</c>.
    /// </summary>
    public static bool IsEntityBoundaryHeading(string line)
    {
        if (!AnyHeading.IsMatch(line))
        {
            return false;
        }

        string text = StripHeading(line).Trim();

        // Traço de ficha mal marcado como heading: "### Ataque Múltiplo. O dragão faz três..."
        if (text.Length > 55 || text.Contains(". ", StringComparison.Ordinal))
        {
            return false;
        }

        string norm = text.TrimEnd('.', ':').Trim();
        if (norm.StartsWith("VARIAÇÃO", StringComparison.OrdinalIgnoreCase)
            || norm.StartsWith("VARIANTE", StringComparison.OrdinalIgnoreCase)
            || norm.StartsWith("OPCIONAL", StringComparison.OrdinalIgnoreCase)
            || norm.StartsWith("OPÇÃO", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !StatBlockSubheadings.Contains(norm, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// <c>true</c> se a linha parece o nome de uma entidade em CAIXA ALTA: toda palavra
    /// alfabética de 2+ letras está em maiúsculas (conectivos minúsculos são tolerados),
    /// e existe pelo menos uma letra.
    /// </summary>
    public static bool LooksLikeCapsName(string line)
    {
        string s = StripHeading(line).Trim();
        if (s.Length == 0 || s.Length > 80)
        {
            return false;
        }

        bool sawLetter = false;
        foreach (string word in s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            string letters = new string(word.Where(char.IsLetter).ToArray());
            if (letters.Length < 2)
            {
                continue;
            }

            sawLetter = true;
            if (Connectives.Contains(letters))
            {
                continue;
            }

            // Tolera 1 minúscula por palavra (ruído de OCR: "MíMICO", "PdMs").
            if (letters.Count(char.IsLower) > 1)
            {
                return false;
            }
        }

        return sawLetter;
    }

    /// <summary>Normaliza um nome cru: remove heading, colapsa espaços, tira pontuação nas pontas.</summary>
    public static string CleanName(string raw)
    {
        string s = StripHeading(raw);
        s = Whitespace.Replace(s, " ").Trim().Trim('.', ',', ':', ';', '·', '-', '—', '*').Trim();
        return s;
    }

    /// <summary>Converte "ABÓBORA DA MORTE" -> "Abóbora da Morte" (armazenamento mais limpo).</summary>
    public static string ToTitleCase(string caps)
    {
        string lower = caps.ToLower(PtBr);
        string[] words = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (i > 0 && Connectives.Contains(words[i]))
            {
                continue;
            }

            words[i] = CapitalizeFirst(words[i]);
        }

        return string.Join(' ', words);
    }

    private static string CapitalizeFirst(string word)
    {
        // Preserva hífen: "fogo-fátuo" -> "Fogo-Fátuo".
        string[] parts = word.Split('-');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 0)
            {
                parts[i] = char.ToUpper(parts[i][0], PtBr) + parts[i].Substring(1);
            }
        }

        return string.Join('-', parts);
    }

    /// <summary>Linhas de um bloco (um parágrafo tem várias; uma tabela conta como uma linha só).</summary>
    public static List<string> LinesOf(DocumentBlock block)
        => block is ParagraphBlock p ? p.Lines : new List<string> { block.Text };

    /// <summary>
    /// Índice do primeiro bloco cuja <b>primeira linha</b> é um heading Markdown cujo texto
    /// começa por <paramref name="headingText"/> (case-insensitive), ou -1. Evita casar com
    /// menções da palavra no meio de um parágrafo (ex.: "outros apêndices").
    /// </summary>
    public static int IndexOfHeadingStartingWith(
        List<DocumentBlock> blocks, string headingText, int from = 0)
    {
        for (int i = Math.Max(0, from); i < blocks.Count; i++)
        {
            if (blocks[i] is ParagraphBlock p
                && p.Lines.Count > 0
                && AnyHeading.IsMatch(p.Lines[0])
                && StripHeading(p.Lines[0]).StartsWith(headingText, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
