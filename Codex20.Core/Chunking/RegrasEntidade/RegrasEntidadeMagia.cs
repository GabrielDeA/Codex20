using System.Text.RegularExpressions;
using Codex20.Core.PreProcessamento;
using static Codex20.Core.Chunking.RegrasEntidade.AuxiliaresRegrasEntidade;

namespace Codex20.Core.Chunking.RegrasEntidade;

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
internal class RegrasEntidadeMagia : IRegrasEntidade
{
    private static readonly Regex RegexLinhaDescritor = new(
        @"^\s*#{0,6}\s*(Truque de|\d+\s*[º°]\s*n[íi]vel de)\s+" +
        @"(abjuração|adivinhação|conjuração|encantamento|evocação|ilusão|necromancia|transmutação)" +
        @"(\s*\(ritual\))?\s*$",
        RegexOptions.IgnoreCase);

    private static readonly Regex RegexLinhaTempoDeConjuracao = new(@"^Tempo de Conjuração:", RegexOptions.IgnoreCase);

    public string Nome => "entity-aware/magia";

    public int MaxTokensPorChunk => 2000;

    public bool IsHeadingFronteira(string linha) => IsLinhaHeading(linha);

    public (int Inicio, int Fim) ResolverSecao(List<BlocoDocumento> blocos)
    {
        int inicio = IndiceDoHeadingComecandoCom(blocos, "DESCRIÇÕES DAS MAGIAS");
        if (inicio < 0)
        {
            return (0, blocos.Count);
        }

        int fim = IndiceDoHeadingComecandoCom(blocos, "APÊNDICE", inicio + 1);
        return (inicio + 1, fim < 0 ? blocos.Count : fim);
    }

    public bool IsAncora(List<BlocoDocumento> blocos, int indice)
    {
        if (blocos[indice] is not BlocoParagrafo p)
        {
            return false;
        }

        int indiceDescritor = AcharDescritor(p.Linhas);
        if (indiceDescritor < 0)
        {
            return false;
        }

        // "Tempo de Conjuração" nas linhas seguintes do mesmo bloco...
        for (int k = indiceDescritor + 1; k < Math.Min(p.Linhas.Count, indiceDescritor + 5); k++)
        {
            if (RegexLinhaTempoDeConjuracao.IsMatch(p.Linhas[k]))
            {
                return true;
            }
        }

        // ...ou no início do bloco seguinte.
        if (indice + 1 < blocos.Count)
        {
            List<string> proximo = LinhasDe(blocos[indice + 1]);
            for (int k = 0; k < Math.Min(proximo.Count, 3); k++)
            {
                if (RegexLinhaTempoDeConjuracao.IsMatch(proximo[k]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public int AcharInicioCabecalho(List<BlocoDocumento> blocos, int indiceAncora)
    {
        List<string> linhas = LinhasDe(blocos[indiceAncora]);
        int indiceDescritor = AcharDescritor(linhas);

        // Descritor não é a primeira linha do bloco → o nome está neste mesmo bloco.
        if (indiceDescritor > 0)
        {
            return indiceAncora;
        }

        // Descritor abre o bloco → o nome está no parágrafo anterior.
        return Math.Max(0, indiceAncora - 1);
    }

    public string? ExtrairNomeEntidade(List<BlocoDocumento> blocos, int inicioCabecalho, int indiceAncora)
    {
        List<string> linhasAncora = LinhasDe(blocos[indiceAncora]);
        int indiceDescritor = AcharDescritor(linhasAncora);

        // Nome nas linhas do próprio bloco-âncora, acima do descritor.
        for (int k = indiceDescritor - 1; k >= 0; k--)
        {
            if (IsNomeEmCaixaAlta(linhasAncora[k]))
            {
                return ParaTitleCase(LimparNome(linhasAncora[k]));
            }
        }

        // Nome no(s) bloco(s) de cabeçalho anteriores.
        for (int i = indiceAncora - 1; i >= inicioCabecalho && i >= 0; i--)
        {
            List<string> linhas = LinhasDe(blocos[i]);
            for (int k = linhas.Count - 1; k >= 0; k--)
            {
                if (IsNomeEmCaixaAlta(linhas[k]))
                {
                    return ParaTitleCase(LimparNome(linhas[k]));
                }
            }
        }

        return null;
    }

    private static int AcharDescritor(List<string> linhas)
    {
        for (int i = 0; i < linhas.Count; i++)
        {
            if (RegexLinhaDescritor.IsMatch(linhas[i]))
            {
                return i;
            }
        }

        return -1;
    }
}
