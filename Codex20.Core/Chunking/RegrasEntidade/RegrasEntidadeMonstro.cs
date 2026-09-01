using System.Text.RegularExpressions;
using Codex20.Core.PreProcessamento;
using static Codex20.Core.Chunking.RegrasEntidade.AuxiliaresRegrasEntidade;

namespace Codex20.Core.Chunking.RegrasEntidade;

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
internal class RegrasEntidadeMonstro : IRegrasEntidade
{
    private static readonly Regex LinhaClasseDeArmadura = new(@"^Classe de Armadura\s+\d", RegexOptions.IgnoreCase);

    private static readonly Regex LinhaPontosDeVida = new(@"^Pontos de Vida\s+\d", RegexOptions.IgnoreCase);

    /// <summary>
    /// Linha-tipo da criatura: <c>&lt;Tipo&gt; &lt;Tamanho&gt;[ (&lt;subtipo&gt;)], &lt;alinhamento&gt;</c>.
    /// O Tipo é sempre Title Case ("Dragão", "Morto-vivo") — case-sensitive de propósito, para
    /// não confundir com o nome em CAIXA ALTA ("DRAGÃO AZUL ADULTO"). O Tamanho pode vir em
    /// minúsculas ("Besta pequena"), então só essa parte é case-insensitive.
    /// </summary>
    private static readonly Regex LinhaTipo = new(
        @"(?<nome>.*?)\b(?<tipo>Aberração|Besta|Celestial|Constructo|Corruptor|Drag(ão|ões)|Elemental|Enxame|Fada|Gigante|Humanoide|Limo|Monstruosidade|Morto-vivo|Planta)\b" +
        @"[^,]*?\s(?i:Min[úu]scul[oa]|Mi[úu]d[oa]|Pequen[oa]|M[ée]di[oa]|Grande|Enorme|Imens[oa]|Colossal)\b[^,]*,\s*\S");

    public string Nome => "entity-aware/monstro";

    public int MaxTokensPorChunk => 6000;

    // Criaturas ocupam quase o livro inteiro; sem gate de seção.
    public (int Inicio, int Fim) ResolverSecao(List<BlocoDocumento> blocos) => (0, blocos.Count);

    public bool IsHeadingFronteira(string linha) => IsHeadingFronteiraEntidade(linha);

    public bool IsAncora(List<BlocoDocumento> blocos, int indice)
    {
        if (blocos[indice] is not BlocoParagrafo p)
        {
            return false;
        }

        for (int k = 0; k < p.Linhas.Count; k++)
        {
            if (!LinhaClasseDeArmadura.IsMatch(p.Linhas[k]))
            {
                continue;
            }

            // "Pontos de Vida" logo abaixo, no mesmo bloco...
            if (k + 1 < p.Linhas.Count && LinhaPontosDeVida.IsMatch(p.Linhas[k + 1]))
            {
                return true;
            }

            // ...ou no início do bloco seguinte.
            if (indice + 1 < blocos.Count)
            {
                List<string> proximo = LinhasDe(blocos[indice + 1]);
                if (proximo.Count > 0 && LinhaPontosDeVida.IsMatch(proximo[0]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public int AcharInicioCabecalho(List<BlocoDocumento> blocos, int indiceAncora)
    {
        // (a) Sobe até 5 parágrafos procurando a linha-tipo da ficha desta criatura.
        int cabecalhoAtributos = -1;
        for (int i = indiceAncora - 1; i >= 0 && i >= indiceAncora - 5; i--)
        {
            List<string> linhas = LinhasDe(blocos[i]);
            int indiceLinhaTipo = AcharLinhaTipo(linhas);
            if (indiceLinhaTipo < 0)
            {
                continue;
            }

            // Linha-tipo sem nome inline e sem nome acima → o nome está no bloco anterior.
            Match m = LinhaTipo.Match(linhas[indiceLinhaTipo]);
            bool nomeInline = LimparNome(m.Groups["nome"].Value).Length > 0;
            bool nomeAcima = indiceLinhaTipo > 0 && IsNomeEmCaixaAlta(linhas[indiceLinhaTipo - 1]);
            bool nomeNoBlocoAnterior = !nomeInline && !nomeAcima
                && i - 1 >= 0 && IsNomeEmCaixaAlta(UltimaLinha(blocos[i - 1]));

            cabecalhoAtributos = nomeNoBlocoAnterior ? i - 1 : i;
            break;
        }

        if (cabecalhoAtributos < 0)
        {
            return indiceAncora - 1; // ficha-variante sem cabeçalho próprio
        }

        // (b) Continua subindo por parágrafos de lore até o heading do capítulo da criatura.
        // Para se cruzar a ficha da criatura ANTERIOR (âncora, linha-tipo ou sub-cabeçalho).
        for (int j = cabecalhoAtributos - 1; j >= 0 && j >= cabecalhoAtributos - 15; j--)
        {
            if (IsAncora(blocos, j))
            {
                break;
            }

            List<string> linhas = LinhasDe(blocos[j]);
            if (linhas.Count == 0)
            {
                continue;
            }

            if (AcharLinhaTipo(linhas) >= 0)
            {
                break;
            }

            // Sub-cabeçalho de ficha (# AÇÕES, ## REAÇÕES...) → é da criatura anterior; para.
            if (IsLinhaHeading(linhas[0]) && !IsHeadingFronteiraEntidade(linhas[0]))
            {
                break;
            }

            if (IsHeadingFronteiraEntidade(linhas[0]))
            {
                return j;
            }
        }

        return cabecalhoAtributos;
    }

    public string? ExtrairNomeEntidade(List<BlocoDocumento> blocos, int inicioCabecalho, int indiceAncora)
    {
        // Varre do cabeçalho até a âncora atrás da linha-tipo; ignora falsos positivos na lore.
        for (int i = inicioCabecalho; i <= indiceAncora && i < blocos.Count; i++)
        {
            List<string> linhas = LinhasDe(blocos[i]);
            for (int k = 0; k < linhas.Count; k++)
            {
                if (!LinhaTipo.IsMatch(linhas[k]))
                {
                    continue;
                }

                // 1. Nome inline, antes do tipo, na própria linha-tipo.
                string inline = LimparNome(LinhaTipo.Match(linhas[k]).Groups["nome"].Value);
                if (inline.Length > 1 && IsNomeEmCaixaAlta(inline))
                {
                    return ParaTitleCase(inline);
                }

                // 2. Linha(s) em CAIXA ALTA imediatamente acima (mesmo bloco).
                string? acima = NomeEmCaixaAltaTerminandoEm(linhas, k - 1);
                if (acima != null)
                {
                    return ParaTitleCase(acima);
                }

                // 3. Última(s) linha(s) do bloco anterior.
                if (i - 1 >= 0)
                {
                    List<string> anterior = LinhasDe(blocos[i - 1]);
                    string? nomeAnterior = NomeEmCaixaAltaTerminandoEm(anterior, anterior.Count - 1);
                    if (nomeAnterior != null)
                    {
                        return ParaTitleCase(nomeAnterior);
                    }
                }

                // 4. Heading do capítulo (ex.: "## OROG", "## POVO LAGARTO").
                return NomeDoHeadingDeCapitulo(blocos, inicioCabecalho);
            }
        }

        // Nenhuma linha-tipo achada mas o cabeçalho pode ser um heading em CAIXA ALTA.
        return NomeDoHeadingDeCapitulo(blocos, inicioCabecalho);
    }

    private static string? NomeDoHeadingDeCapitulo(List<BlocoDocumento> blocos, int inicioCabecalho)
    {
        List<string> linhas = LinhasDe(blocos[inicioCabecalho]);
        if (linhas.Count == 0)
        {
            return null;
        }

        string linhaCabecalho = linhas[0];
        if (IsHeadingFronteiraEntidade(linhaCabecalho) && IsNomeEmCaixaAlta(linhaCabecalho))
        {
            return ParaTitleCase(LimparNome(linhaCabecalho));
        }

        return null;
    }

    private static int AcharLinhaTipo(List<string> linhas)
    {
        for (int i = 0; i < linhas.Count; i++)
        {
            if (LinhaTipo.IsMatch(linhas[i]))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Junta 1–2 linhas em CAIXA ALTA terminando no índice <paramref name="fim"/>.</summary>
    private static string? NomeEmCaixaAltaTerminandoEm(List<string> linhas, int fim)
    {
        if (fim < 0 || fim >= linhas.Count || !IsNomeEmCaixaAlta(linhas[fim]))
        {
            return null;
        }

        string nome = RemoverHeading(linhas[fim]).Trim();
        if (fim - 1 >= 0 && IsNomeEmCaixaAlta(linhas[fim - 1]) && !LinhaTipo.IsMatch(linhas[fim - 1]))
        {
            nome = RemoverHeading(linhas[fim - 1]).Trim() + " " + nome;
        }

        return LimparNome(nome);
    }

    private static string UltimaLinha(BlocoDocumento bloco)
    {
        List<string> linhas = LinhasDe(bloco);
        return linhas.Count > 0 ? linhas[linhas.Count - 1] : string.Empty;
    }
}
