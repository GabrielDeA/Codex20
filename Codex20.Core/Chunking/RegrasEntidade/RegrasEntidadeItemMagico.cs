using System.Text.RegularExpressions;
using Codex20.Core.PreProcessamento;
using static Codex20.Core.Chunking.RegrasEntidade.AuxiliaresRegrasEntidade;

namespace Codex20.Core.Chunking.RegrasEntidade;

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
internal class RegrasEntidadeItemMagico : IRegrasEntidade
{
    private static readonly Regex LinhaDescritor = new(
        @"^\s*#{0,6}\s*(?<nome>.*?)\s*\b(?<tipo>Item maravilhoso|Anel|Armadura|Arma|Poção|Pergaminho|Varinha|Cajado|Bastão|Haste)\b" +
        @"(\s*\([^)]*\))?,\s*(?<raridade>comum|incomum|rar[oa]|muito rar[oa]|lend[áa]ri[oa]|artefato)\b" +
        @"(\s*\(requer sintoniza\w+[^)]*\))?" +
        @"(\s*\(\+\d\).*)?$", // tolera a cauda "(+1), rara (+2) ou muito rara" das entradas genéricas +X
        RegexOptions.IgnoreCase);

    public string Nome => "entity-aware/item-magico";

    public int MaxTokensPorChunk => 2500;

    public bool IsHeadingFronteira(string linha) => IsLinhaHeading(linha);

    public (int Inicio, int Fim) ResolverSecao(List<BlocoDocumento> blocos)
    {
        int inicio = IndiceDoHeadingComecandoCom(blocos, "ITENS MÁGICOS DE A-Z");
        if (inicio < 0)
        {
            return (0, blocos.Count);
        }

        int fim = IndiceDoHeadingComecandoCom(blocos, "ITENS MÁGICOS INTELIGENTES", inicio + 1);
        return (inicio + 1, fim < 0 ? blocos.Count : fim);
    }

    public bool IsAncora(List<BlocoDocumento> blocos, int indice)
    {
        if (blocos[indice] is not BlocoParagrafo p || p.Linhas.Count == 0)
        {
            return false;
        }

        // O descritor abre a entrada (linha 0) ou é a 1ª linha após o heading do nome.
        if (LinhaDescritor.IsMatch(p.Linhas[0]))
        {
            return true;
        }

        return p.Linhas.Count > 1 && LinhaDescritor.IsMatch(p.Linhas[1]) && IsNomeEmCaixaAlta(p.Linhas[0]);
    }

    public int AcharInicioCabecalho(List<BlocoDocumento> blocos, int indiceAncora)
    {
        List<string> linhas = LinhasDe(blocos[indiceAncora]);
        Match m = LinhaDescritor.Match(linhas[0]);
        bool nomeInline = m.Success && LimparNome(m.Groups["nome"].Value).Length > 1;
        bool nomeNoMesmoBloco = linhas.Count > 1 && IsNomeEmCaixaAlta(linhas[0]);

        if (nomeInline || nomeNoMesmoBloco)
        {
            return indiceAncora;
        }

        // Descritor sozinho → nome está no parágrafo (heading) anterior.
        return Math.Max(0, indiceAncora - 1);
    }

    public string? ExtrairNomeEntidade(List<BlocoDocumento> blocos, int inicioCabecalho, int indiceAncora)
    {
        List<string> linhas = LinhasDe(blocos[indiceAncora]);

        // 1. Nome inline, antes do tipo, na linha-descritor.
        Match m = LinhaDescritor.Match(linhas[0]);
        if (m.Success)
        {
            string inline = LimparNome(m.Groups["nome"].Value);
            if (inline.Length > 1 && IsNomeEmCaixaAlta(inline))
            {
                return ParaTitleCase(inline);
            }
        }

        // 2. Linha em CAIXA ALTA acima do descritor, no mesmo bloco.
        if (linhas.Count > 1 && LinhaDescritor.IsMatch(linhas[1]) && IsNomeEmCaixaAlta(linhas[0]))
        {
            return ParaTitleCase(LimparNome(linhas[0]));
        }

        // 3. Heading do bloco anterior.
        for (int i = indiceAncora - 1; i >= inicioCabecalho && i >= 0; i--)
        {
            List<string> anterior = LinhasDe(blocos[i]);
            for (int k = anterior.Count - 1; k >= 0; k--)
            {
                if (IsNomeEmCaixaAlta(anterior[k]))
                {
                    return ParaTitleCase(LimparNome(anterior[k]));
                }
            }
        }

        return null;
    }
}
