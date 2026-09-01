using System.Globalization;
using System.Text.RegularExpressions;
using Codex20.Core.PreProcessamento;

namespace Codex20.Core.Chunking.RegrasEntidade;

/// <summary>Utilitários compartilhados pelas regras de entidade dos três livros.</summary>
internal static class AuxiliaresRegrasEntidade
{
    private static readonly Regex RegexPrefixoHeading = new(@"^#{1,6}\s+");

    private static readonly Regex RegexQualquerHeading = new(@"^#{1,6}\s+\S");

    private static readonly Regex RegexEspacosEmBranco = new(@"\s+");

    /// <summary>Conectivos que podem aparecer em minúsculas no meio de um nome em CAIXA ALTA.</summary>
    private static readonly HashSet<string> Conectivos =
        new(StringComparer.OrdinalIgnoreCase) { "de", "do", "da", "dos", "das", "e", "ou", "a", "o", "the", "of" };

    /// <summary>
    /// Sub-cabeçalhos do bloco de atributos de uma criatura que o Document Intelligence às
    /// vezes marca como heading Markdown (<c># AÇÕES</c>, <c>## REAÇÕES</c>, ...). Não são
    /// fronteira de entidade — pertencem à ficha da criatura corrente.
    /// </summary>
    private static readonly string[] SubHeadingsBlocoAtributos =
    {
        "AÇÕES", "AÇOES", "REAÇÕES", "REAÇOES",
        "AÇÕES LENDÁRIAS", "AÇÕES LENDARIAS", "AÇÕES DE COVIL",
        "AÇÕES DE COVIL E EFEITOS REGIONAIS", "EFEITOS REGIONAIS",
        "AÇÕES ADICIONAIS", "AÇÕES BÔNUS",
    };

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string RemoverHeading(string linha) => RegexPrefixoHeading.Replace(linha, string.Empty).Trim();

    public static bool IsLinhaHeading(string linha) => RegexQualquerHeading.IsMatch(linha);

    /// <summary>
    /// Heading que de fato inicia outra criatura/grupo. Exclui:
    /// sub-cabeçalhos de ficha (<c># AÇÕES</c>); traços de criatura que o Document
    /// Intelligence marcou como heading por engano (<c>### Nome. Texto longo...</c>);
    /// e sidebars <c>VARIAÇÃO:</c> / <c>OPCIONAL:</c>.
    /// </summary>
    public static bool IsHeadingFronteiraEntidade(string linha)
    {
        if (!RegexQualquerHeading.IsMatch(linha))
        {
            return false;
        }

        string texto = RemoverHeading(linha).Trim();

        // Traço de ficha mal marcado como heading: "### Ataque Múltiplo. O dragão faz três..."
        if (texto.Length > 55 || texto.Contains(". ", StringComparison.Ordinal))
        {
            return false;
        }

        string norm = texto.TrimEnd('.', ':').Trim();
        if (norm.StartsWith("VARIAÇÃO", StringComparison.OrdinalIgnoreCase)
            || norm.StartsWith("VARIANTE", StringComparison.OrdinalIgnoreCase)
            || norm.StartsWith("OPCIONAL", StringComparison.OrdinalIgnoreCase)
            || norm.StartsWith("OPÇÃO", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !SubHeadingsBlocoAtributos.Contains(norm, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// <c>true</c> se a linha parece o nome de uma entidade em CAIXA ALTA: toda palavra
    /// alfabética de 2+ letras está em maiúsculas (conectivos minúsculos são tolerados),
    /// e existe pelo menos uma letra.
    /// </summary>
    public static bool IsNomeEmCaixaAlta(string linha)
    {
        string s = RemoverHeading(linha).Trim();
        if (s.Length == 0 || s.Length > 80)
        {
            return false;
        }

        bool viuLetra = false;
        foreach (string palavra in s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            string letras = new string(palavra.Where(char.IsLetter).ToArray());
            if (letras.Length < 2)
            {
                continue;
            }

            viuLetra = true;
            if (Conectivos.Contains(letras))
            {
                continue;
            }

            // Tolera 1 minúscula por palavra (ruído de OCR: "MíMICO", "PdMs").
            if (letras.Count(char.IsLower) > 1)
            {
                return false;
            }
        }

        return viuLetra;
    }

    /// <summary>Normaliza um nome cru: remove heading, colapsa espaços, tira pontuação nas pontas.</summary>
    public static string LimparNome(string cru)
    {
        string s = RemoverHeading(cru);
        s = RegexEspacosEmBranco.Replace(s, " ").Trim().Trim('.', ',', ':', ';', '·', '-', '—', '*').Trim();
        return s;
    }

    /// <summary>Converte "ABÓBORA DA MORTE" -> "Abóbora da Morte" (armazenamento mais limpo).</summary>
    public static string ParaTitleCase(string caixaAlta)
    {
        string minusculo = caixaAlta.ToLower(PtBr);
        string[] palavras = minusculo.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < palavras.Length; i++)
        {
            if (i > 0 && Conectivos.Contains(palavras[i]))
            {
                continue;
            }

            palavras[i] = CapitalizarPrimeira(palavras[i]);
        }

        return string.Join(' ', palavras);
    }

    private static string CapitalizarPrimeira(string palavra)
    {
        // Preserva hífen: "fogo-fátuo" -> "Fogo-Fátuo".
        string[] partes = palavra.Split('-');
        for (int i = 0; i < partes.Length; i++)
        {
            if (partes[i].Length > 0)
            {
                partes[i] = char.ToUpper(partes[i][0], PtBr) + partes[i].Substring(1);
            }
        }

        return string.Join('-', partes);
    }

    /// <summary>Linhas de um bloco (um parágrafo tem várias; uma tabela conta como uma linha só).</summary>
    public static List<string> LinhasDe(BlocoDocumento bloco)
        => bloco is BlocoParagrafo p ? p.Linhas : new List<string> { bloco.Texto };

    /// <summary>
    /// Índice do primeiro bloco cuja <b>primeira linha</b> é um heading Markdown cujo texto
    /// começa por <paramref name="textoHeading"/> (case-insensitive), ou -1. Evita casar com
    /// menções da palavra no meio de um parágrafo (ex.: "outros apêndices").
    /// </summary>
    public static int IndiceDoHeadingComecandoCom(
        List<BlocoDocumento> blocos, string textoHeading, int de = 0)
    {
        for (int i = Math.Max(0, de); i < blocos.Count; i++)
        {
            if (blocos[i] is BlocoParagrafo p
                && p.Linhas.Count > 0
                && RegexQualquerHeading.IsMatch(p.Linhas[0])
                && RemoverHeading(p.Linhas[0]).StartsWith(textoHeading, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
