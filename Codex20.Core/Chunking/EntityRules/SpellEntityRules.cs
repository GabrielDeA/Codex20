using System.Text.RegularExpressions;
using Codex20.Core.Preprocessing;
using static Codex20.Core.Chunking.EntityRules.EntityRuleHelpers;

namespace Codex20.Core.Chunking.EntityRules;

/// <summary>
/// Regras de detecção de entidade para o <b>Livro do Jogador</b> (entidade = magia).
///
/// <para><b>Âncora de atributos</b>: uma linha-descritor de nível/escola
/// (<c>Truque de &lt;escola&gt;</c> ou <c>&lt;N&gt;º nível de &lt;escola&gt;</c>, com sufixo
/// opcional <c>(ritual)</c>) seguida, na mesma linha lógica ou em até ~4 linhas, por
/// <c>Tempo de Conjuração:</c>. 361 ocorrências de "Tempo de Conjuração" no livro completo,
/// todas dentro da seção <c>## DESCRIÇÕES DAS MAGIAS</c>.</para>
///
/// <para><b>Formatos de cabeçalho reais encontrados</b>:
/// <list type="number">
///   <item>Nome em CAIXA ALTA em parágrafo próprio, seguido de bloco de stats:<br/>
///         <c>ACALMAR EMOÇÕES</c> … <c>2º nível de encantamento</c> / <c>Tempo de Conjuração: 1 ação</c></item>
///   <item>Nome, descritor e "Tempo de Conjuração" nas três primeiras linhas do MESMO parágrafo:<br/>
///         <c>ALIADO PLANAR</c> / <c>6º nível de conjuração</c> / <c>Tempo de Conjuração: 10 minutos</c></item>
///   <item>Nome com heading Markdown:<br/>
///         <c>## ANIMAR MORTOS</c> / <c>3º nível de necromancia</c></item>
/// </list>
/// Inconsistências reais: o ordinal aparece como <c>º</c> (U+00BA) e como <c>°</c> (U+00B0);
/// "nível" às vezes sem acento; linhas em branco espúrias fragmentam o bloco de stats.
/// As listas de magia por classe (<c>Bola de Fogo (evocação)</c>) NÃO são entradas — não têm
/// bloco de stats e o gate de seção as exclui.</para>
///
/// <para>Escolas: abjuração, adivinhação, conjuração, encantamento, evocação, ilusão,
/// necromancia, transmutação.</para>
///
/// <para><b>Resultado na validação (livro completo):</b> 361 magias detectadas (bate com as
/// 361 ocorrências de "Tempo de Conjuração"), 100% com nome limpo, 0 tabelas cortadas.</para>
///
/// <para><b>Limitação conhecida</b>: características de classe e talentos não têm âncora
/// confiável (prosa com subtítulos em CAIXA ALTA) e ficam no fallback.</para>
/// </summary>
internal class SpellEntityRules : IEntityRules
{
    private static readonly Regex DescriptorLine = new(
        @"^\s*#{0,6}\s*(Truque de|\d+\s*[º°]\s*n[íi]vel de)\s+" +
        @"(abjuração|adivinhação|conjuração|encantamento|evocação|ilusão|necromancia|transmutação)" +
        @"(\s*\(ritual\))?\s*$",
        RegexOptions.IgnoreCase);

    private static readonly Regex CastingTimeLine = new(@"^Tempo de Conjuração:", RegexOptions.IgnoreCase);

    public string Name => "entity-aware/magia";

    public int MaxTokensPerChunk => 2000;

    public bool IsBoundaryHeading(string line) => IsHeadingLine(line);

    public (int Start, int End) ResolveSection(List<DocumentBlock> blocks)
    {
        int start = IndexOfHeadingStartingWith(blocks, "DESCRIÇÕES DAS MAGIAS");
        if (start < 0)
        {
            return (0, blocks.Count);
        }

        int end = IndexOfHeadingStartingWith(blocks, "APÊNDICE", start + 1);
        return (start + 1, end < 0 ? blocks.Count : end);
    }

    public bool IsAnchor(List<DocumentBlock> blocks, int index)
    {
        if (blocks[index] is not ParagraphBlock p)
        {
            return false;
        }

        int descriptorIdx = FindDescriptor(p.Lines);
        if (descriptorIdx < 0)
        {
            return false;
        }

        // "Tempo de Conjuração" nas linhas seguintes do mesmo bloco...
        for (int k = descriptorIdx + 1; k < Math.Min(p.Lines.Count, descriptorIdx + 5); k++)
        {
            if (CastingTimeLine.IsMatch(p.Lines[k]))
            {
                return true;
            }
        }

        // ...ou no início do bloco seguinte.
        if (index + 1 < blocks.Count)
        {
            List<string> next = LinesOf(blocks[index + 1]);
            for (int k = 0; k < Math.Min(next.Count, 3); k++)
            {
                if (CastingTimeLine.IsMatch(next[k]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public int FindHeaderStart(List<DocumentBlock> blocks, int anchorIndex)
    {
        List<string> lines = LinesOf(blocks[anchorIndex]);
        int descriptorIdx = FindDescriptor(lines);

        // Descritor não é a primeira linha do bloco → o nome está neste mesmo bloco.
        if (descriptorIdx > 0)
        {
            return anchorIndex;
        }

        // Descritor abre o bloco → o nome está no parágrafo anterior.
        return Math.Max(0, anchorIndex - 1);
    }

    public string? ExtractEntityName(List<DocumentBlock> blocks, int headerStart, int anchorIndex)
    {
        List<string> anchorLines = LinesOf(blocks[anchorIndex]);
        int descriptorIdx = FindDescriptor(anchorLines);

        // Nome nas linhas do próprio bloco-âncora, acima do descritor.
        for (int k = descriptorIdx - 1; k >= 0; k--)
        {
            if (LooksLikeCapsName(anchorLines[k]))
            {
                return ToTitleCase(CleanName(anchorLines[k]));
            }
        }

        // Nome no(s) bloco(s) de cabeçalho anteriores.
        for (int i = anchorIndex - 1; i >= headerStart && i >= 0; i--)
        {
            List<string> lines = LinesOf(blocks[i]);
            for (int k = lines.Count - 1; k >= 0; k--)
            {
                if (LooksLikeCapsName(lines[k]))
                {
                    return ToTitleCase(CleanName(lines[k]));
                }
            }
        }

        return null;
    }

    private static int FindDescriptor(List<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (DescriptorLine.IsMatch(lines[i]))
            {
                return i;
            }
        }

        return -1;
    }
}
