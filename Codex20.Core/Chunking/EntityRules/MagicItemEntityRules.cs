using System.Text.RegularExpressions;
using Codex20.Core.Preprocessing;
using static Codex20.Core.Chunking.EntityRules.EntityRuleHelpers;

namespace Codex20.Core.Chunking.EntityRules;

/// <summary>
/// Regras de detecção de entidade para o <b>Guia do Mestre</b> (entidade = item mágico).
///
/// <para><b>Âncora de atributos</b>: a linha-descritor de tipo + raridade
/// (<c>&lt;tipo&gt;[ (&lt;qualificador&gt;)], &lt;raridade&gt;[ (requer sintonização...)]</c>),
/// que abre a entrada ou vem logo abaixo do nome. ~264 ocorrências no livro; o grosso está
/// na seção <c>## ITENS MÁGICOS DE A-Z</c>.</para>
///
/// <para><b>Formatos de cabeçalho reais encontrados</b>:
/// <list type="number">
///   <item>Nome + descritor na MESMA linha, com heading Markdown de nível variável:<br/>
///         <c>## ALGEMAS DIMENSIONAIS Item maravilhoso, raro</c><br/>
///         <c>#### AMULETO DE SAÚDE Item maravilhoso, raro (requer sintonização)</c></item>
///   <item>Nome em heading próprio, descritor em parágrafo separado logo abaixo:<br/>
///         <c>### AMULETO DE PROTEÇÃO CONTRA DETECÇÃO E LOCALIZAÇÃO</c><br/>
///         <c>Item maravilhoso, incomum (requer sintonização)</c></item>
///   <item>Idem para artefatos com descritor inline:<br/>
///         <c>### ORBE DOS DRAGÕES Item maravilhoso, artefato (requer sintonização)</c></item>
/// </list>
/// Inconsistências reais: o nível do heading Markdown varia de <c>##</c> a <c>######</c> (ou
/// nenhum); o Guia do Mestre escreve "sintoniz<b>ação</b>" (o Manual/Livro do Jogador usam
/// "sintonia"); a raridade aparece no masculino/feminino
/// (<c>raro</c>/<c>rara</c>, <c>lendário</c>/<c>lendária</c>).</para>
///
/// <para>Tipos: Item maravilhoso, Anel, Arma [(qualificador)], Armadura [(qualificador)],
/// Poção, Pergaminho, Varinha, Cajado, Bastão, Haste.
/// Raridades: comum, incomum, raro/rara, muito raro/rara, lendário/lendária, artefato.</para>
///
/// <para><b>Resultado na validação (livro completo):</b> 226 itens detectados (~223 linhas-
/// descritor na seção A-Z + 5 entradas genéricas "+1, +2 ou +3"), 100% com nome limpo,
/// 0 tabelas cortadas.</para>
///
/// <para><b>Limitação conhecida</b>: itens mágicos inteligentes e artefatos nomeados sem
/// linha-descritor (ex.: <c>LIVRO DA ESCURIDÃO PERVERSA</c>), além de armas de cerco e de
/// fogo, têm formato próprio e ficam no fallback (a seção termina em "ITENS MÁGICOS
/// INTELIGENTES").</para>
/// </summary>
internal class MagicItemEntityRules : IEntityRules
{
    private static readonly Regex DescriptorLine = new(
        @"^\s*#{0,6}\s*(?<name>.*?)\s*\b(?<type>Item maravilhoso|Anel|Armadura|Arma|Poção|Pergaminho|Varinha|Cajado|Bastão|Haste)\b" +
        @"(\s*\([^)]*\))?,\s*(?<rar>comum|incomum|rar[oa]|muito rar[oa]|lend[áa]ri[oa]|artefato)\b" +
        @"(\s*\(requer sintoniza\w+[^)]*\))?" +
        @"(\s*\(\+\d\).*)?$", // tolera a cauda "(+1), rara (+2) ou muito rara" das entradas genéricas +X
        RegexOptions.IgnoreCase);

    public string Name => "entity-aware/item-magico";

    public int MaxTokensPerChunk => 2500;

    public bool IsBoundaryHeading(string line) => IsHeadingLine(line);

    public (int Start, int End) ResolveSection(List<DocumentBlock> blocks)
    {
        int start = IndexOfHeadingStartingWith(blocks, "ITENS MÁGICOS DE A-Z");
        if (start < 0)
        {
            return (0, blocks.Count);
        }

        int end = IndexOfHeadingStartingWith(blocks, "ITENS MÁGICOS INTELIGENTES", start + 1);
        return (start + 1, end < 0 ? blocks.Count : end);
    }

    public bool IsAnchor(List<DocumentBlock> blocks, int index)
    {
        if (blocks[index] is not ParagraphBlock p || p.Lines.Count == 0)
        {
            return false;
        }

        // O descritor abre a entrada (linha 0) ou é a 1ª linha após o heading do nome.
        if (DescriptorLine.IsMatch(p.Lines[0]))
        {
            return true;
        }

        return p.Lines.Count > 1 && DescriptorLine.IsMatch(p.Lines[1]) && LooksLikeCapsName(p.Lines[0]);
    }

    public int FindHeaderStart(List<DocumentBlock> blocks, int anchorIndex)
    {
        List<string> lines = LinesOf(blocks[anchorIndex]);
        Match m = DescriptorLine.Match(lines[0]);
        bool nameInline = m.Success && CleanName(m.Groups["name"].Value).Length > 1;
        bool nameInSameBlock = lines.Count > 1 && LooksLikeCapsName(lines[0]);

        if (nameInline || nameInSameBlock)
        {
            return anchorIndex;
        }

        // Descritor sozinho → nome está no parágrafo (heading) anterior.
        return Math.Max(0, anchorIndex - 1);
    }

    public string? ExtractEntityName(List<DocumentBlock> blocks, int headerStart, int anchorIndex)
    {
        List<string> lines = LinesOf(blocks[anchorIndex]);

        // 1. Nome inline, antes do tipo, na linha-descritor.
        Match m = DescriptorLine.Match(lines[0]);
        if (m.Success)
        {
            string inline = CleanName(m.Groups["name"].Value);
            if (inline.Length > 1 && LooksLikeCapsName(inline))
            {
                return ToTitleCase(inline);
            }
        }

        // 2. Linha em CAIXA ALTA acima do descritor, no mesmo bloco.
        if (lines.Count > 1 && DescriptorLine.IsMatch(lines[1]) && LooksLikeCapsName(lines[0]))
        {
            return ToTitleCase(CleanName(lines[0]));
        }

        // 3. Heading do bloco anterior.
        for (int i = anchorIndex - 1; i >= headerStart && i >= 0; i--)
        {
            List<string> prev = LinesOf(blocks[i]);
            for (int k = prev.Count - 1; k >= 0; k--)
            {
                if (LooksLikeCapsName(prev[k]))
                {
                    return ToTitleCase(CleanName(prev[k]));
                }
            }
        }

        return null;
    }
}
