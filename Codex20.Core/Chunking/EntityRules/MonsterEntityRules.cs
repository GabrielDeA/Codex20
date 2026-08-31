using System.Text.RegularExpressions;
using Codex20.Core.Preprocessing;
using static Codex20.Core.Chunking.EntityRules.EntityRuleHelpers;

namespace Codex20.Core.Chunking.EntityRules;

/// <summary>
/// Regras de detecção de entidade para o <b>Manual dos Monstros</b> (entidade = criatura).
///
/// <para><b>Âncora de atributos</b> (o que é de fato repetitivo em toda ficha):
/// uma linha <c>Classe de Armadura &lt;n&gt;...</c> imediatamente seguida por
/// <c>Pontos de Vida ...</c>. 428 ocorrências no livro completo; só 1 ficha (variante de
/// stat block que reusa o nome da criatura anterior) não tem cabeçalho próprio.</para>
///
/// <para><b>Formatos de cabeçalho reais encontrados</b> (a linha-tipo é
/// <c>&lt;Tipo&gt; &lt;Tamanho&gt;[ (&lt;subtipo&gt;)], &lt;alinhamento&gt;</c>):
/// <list type="number">
///   <item>Nome em CAIXA ALTA e linha-tipo em linhas consecutivas do mesmo parágrafo,
///         logo acima da âncora:<br/>
///         <c>ABOCANHADOR MATRAQUEANTE</c> / <c>Aberração Média, neutro</c></item>
///   <item>Nome e linha-tipo separados por linha em branco (dois parágrafos):<br/>
///         <c>BANSHEE</c> … <c>Morto-vivo Médio, caótico e mau</c></item>
///   <item>Nome + tipo na MESMA linha, com heading Markdown:<br/>
///         <c>## BRUXA DO MAR Fada Média, caótico e mau</c></item>
/// </list>
/// Inconsistências reais: o Tamanho aparece em maiúscula ou minúscula
/// (<c>Besta pequena</c>) e no masculino/feminino (<c>Imenso</c>/<c>Imensa</c>);
/// o subtipo <c>(titã)</c> pode aparecer antes da vírgula; <c>Enxame de &lt;algo&gt; &lt;Tamanho&gt;</c>.
/// (Alguns poucos casos vinham embrulhados em <c>&lt;figure&gt;</c>/<c>&lt;figcaption&gt;</c> —
/// ex. Cocatriz, Aarakocra, Lâmia — mas o Markdown foi revisado para deixá-los num dos
/// formatos acima.)</para>
///
/// <para>Tipos observados: Aberração, Besta, Celestial, Constructo, Corruptor, Dragão,
/// Elemental, Enxame, Fada, Gigante, Humanoide, Limo, Monstruosidade, Morto-vivo, Planta.</para>
///
/// <para><b>Resultado na validação (livro completo):</b> 425 criaturas detectadas,
/// 424 com nome limpo (99,8%), 0 tabelas cortadas ao meio.</para>
///
/// <para><b>Limitação conhecida</b>: fichas-variante que compartilham a entrada da criatura
/// pai (ex.: "Sacerdotisa Sahuagin"/"Barão Sahuagin" sob "## SAHUAGIN", ou variantes yuan-ti
/// coladas em "## DEUSES SERPENTES") às vezes não têm cabeçalho próprio no Markdown — uma
/// delas (p.272) fica sem nome extraído.</para>
/// </summary>
internal class MonsterEntityRules : IEntityRules
{
    private static readonly Regex ArmorClassLine = new(@"^Classe de Armadura\s+\d", RegexOptions.IgnoreCase);

    private static readonly Regex HitPointsLine = new(@"^Pontos de Vida\s+\d", RegexOptions.IgnoreCase);

    /// <summary>
    /// Linha-tipo da criatura: <c>&lt;Tipo&gt; &lt;Tamanho&gt;[ (&lt;subtipo&gt;)], &lt;alinhamento&gt;</c>.
    /// O Tipo é sempre Title Case ("Dragão", "Morto-vivo") — case-sensitive de propósito, para
    /// não confundir com o nome em CAIXA ALTA ("DRAGÃO AZUL ADULTO"). O Tamanho pode vir em
    /// minúsculas ("Besta pequena"), então só essa parte é case-insensitive.
    /// </summary>
    private static readonly Regex TypeLine = new(
        @"(?<name>.*?)\b(?<type>Aberração|Besta|Celestial|Constructo|Corruptor|Drag(ão|ões)|Elemental|Enxame|Fada|Gigante|Humanoide|Limo|Monstruosidade|Morto-vivo|Planta)\b" +
        @"[^,]*?\s(?i:Min[úu]scul[oa]|Mi[úu]d[oa]|Pequen[oa]|M[ée]di[oa]|Grande|Enorme|Imens[oa]|Colossal)\b[^,]*,\s*\S");

    public string Name => "entity-aware/monstro";

    public int MaxTokensPerChunk => 6000;

    // Criaturas ocupam quase o livro inteiro; sem gate de seção.
    public (int Start, int End) ResolveSection(List<DocumentBlock> blocks) => (0, blocks.Count);

    public bool IsBoundaryHeading(string line) => IsEntityBoundaryHeading(line);

    public bool IsAnchor(List<DocumentBlock> blocks, int index)
    {
        if (blocks[index] is not ParagraphBlock p)
        {
            return false;
        }

        for (int k = 0; k < p.Lines.Count; k++)
        {
            if (!ArmorClassLine.IsMatch(p.Lines[k]))
            {
                continue;
            }

            // "Pontos de Vida" logo abaixo, no mesmo bloco...
            if (k + 1 < p.Lines.Count && HitPointsLine.IsMatch(p.Lines[k + 1]))
            {
                return true;
            }

            // ...ou no início do bloco seguinte.
            if (index + 1 < blocks.Count)
            {
                List<string> next = LinesOf(blocks[index + 1]);
                if (next.Count > 0 && HitPointsLine.IsMatch(next[0]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public int FindHeaderStart(List<DocumentBlock> blocks, int anchorIndex)
    {
        // (a) Sobe até 5 parágrafos procurando a linha-tipo da ficha desta criatura.
        int statHeader = -1;
        for (int i = anchorIndex - 1; i >= 0 && i >= anchorIndex - 5; i--)
        {
            List<string> lines = LinesOf(blocks[i]);
            int typeLineIdx = FindTypeLine(lines);
            if (typeLineIdx < 0)
            {
                continue;
            }

            // Linha-tipo sem nome inline e sem nome acima → o nome está no bloco anterior.
            Match m = TypeLine.Match(lines[typeLineIdx]);
            bool nameInline = CleanName(m.Groups["name"].Value).Length > 0;
            bool nameAbove = typeLineIdx > 0 && LooksLikeCapsName(lines[typeLineIdx - 1]);
            bool nameInPrevBlock = !nameInline && !nameAbove
                && i - 1 >= 0 && LooksLikeCapsName(LastLine(blocks[i - 1]));

            statHeader = nameInPrevBlock ? i - 1 : i;
            break;
        }

        if (statHeader < 0)
        {
            return anchorIndex - 1; // ficha-variante sem cabeçalho próprio
        }

        // (b) Continua subindo por parágrafos de lore até o heading do capítulo da criatura.
        // Para se cruzar a ficha da criatura ANTERIOR (âncora, linha-tipo ou sub-cabeçalho).
        for (int j = statHeader - 1; j >= 0 && j >= statHeader - 15; j--)
        {
            if (IsAnchor(blocks, j))
            {
                break;
            }

            List<string> lines = LinesOf(blocks[j]);
            if (lines.Count == 0)
            {
                continue;
            }

            if (FindTypeLine(lines) >= 0)
            {
                break;
            }

            // Sub-cabeçalho de ficha (# AÇÕES, ## REAÇÕES...) → é da criatura anterior; para.
            if (IsHeadingLine(lines[0]) && !IsEntityBoundaryHeading(lines[0]))
            {
                break;
            }

            if (IsEntityBoundaryHeading(lines[0]))
            {
                return j;
            }
        }

        return statHeader;
    }

    public string? ExtractEntityName(List<DocumentBlock> blocks, int headerStart, int anchorIndex)
    {
        // Varre do cabeçalho até a âncora atrás da linha-tipo; ignora falsos positivos na lore.
        for (int i = headerStart; i <= anchorIndex && i < blocks.Count; i++)
        {
            List<string> lines = LinesOf(blocks[i]);
            for (int k = 0; k < lines.Count; k++)
            {
                if (!TypeLine.IsMatch(lines[k]))
                {
                    continue;
                }

                // 1. Nome inline, antes do tipo, na própria linha-tipo.
                string inline = CleanName(TypeLine.Match(lines[k]).Groups["name"].Value);
                if (inline.Length > 1 && LooksLikeCapsName(inline))
                {
                    return ToTitleCase(inline);
                }

                // 2. Linha(s) em CAIXA ALTA imediatamente acima (mesmo bloco).
                string? above = CapsNameEndingAt(lines, k - 1);
                if (above != null)
                {
                    return ToTitleCase(above);
                }

                // 3. Última(s) linha(s) do bloco anterior.
                if (i - 1 >= 0)
                {
                    List<string> prev = LinesOf(blocks[i - 1]);
                    string? prevName = CapsNameEndingAt(prev, prev.Count - 1);
                    if (prevName != null)
                    {
                        return ToTitleCase(prevName);
                    }
                }

                // 4. Heading do capítulo (ex.: "## OROG", "## POVO LAGARTO").
                return NameFromChapterHeading(blocks, headerStart);
            }
        }

        // Nenhuma linha-tipo achada mas o cabeçalho pode ser um heading em CAIXA ALTA.
        return NameFromChapterHeading(blocks, headerStart);
    }

    private static string? NameFromChapterHeading(List<DocumentBlock> blocks, int headerStart)
    {
        List<string> lines = LinesOf(blocks[headerStart]);
        if (lines.Count == 0)
        {
            return null;
        }

        string headerLine = lines[0];
        if (IsEntityBoundaryHeading(headerLine) && LooksLikeCapsName(headerLine))
        {
            return ToTitleCase(CleanName(headerLine));
        }

        return null;
    }

    private static int FindTypeLine(List<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (TypeLine.IsMatch(lines[i]))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Junta 1–2 linhas em CAIXA ALTA terminando no índice <paramref name="end"/>.</summary>
    private static string? CapsNameEndingAt(List<string> lines, int end)
    {
        if (end < 0 || end >= lines.Count || !LooksLikeCapsName(lines[end]))
        {
            return null;
        }

        string name = StripHeading(lines[end]).Trim();
        if (end - 1 >= 0 && LooksLikeCapsName(lines[end - 1]) && !TypeLine.IsMatch(lines[end - 1]))
        {
            name = StripHeading(lines[end - 1]).Trim() + " " + name;
        }

        return CleanName(name);
    }

    private static string LastLine(DocumentBlock block)
    {
        List<string> lines = LinesOf(block);
        return lines.Count > 0 ? lines[lines.Count - 1] : string.Empty;
    }
}
