using System.Text.RegularExpressions;

namespace Codex20.Core.Geracao;

/// <summary>
/// Acha as citações [n] no texto do modelo. Uma regex só, usada tanto para contar o que foi citado
/// quanto para a tela transformar citação em link — as duas nunca discordam.
/// </summary>
public static class LeitorCitacoes
{
    /// <summary>
    /// "[2]", e também "[1, 3]" / "[1; 3]": pedimos "[1][3]", mas o modelo às vezes junta.
    /// Colchetes seguidos ("[1][3]") casam como duas citações vizinhas.
    /// </summary>
    private static readonly Regex RegexCitacao = new(@"\[(\d+(?:\s*[,;]\s*\d+)*)\]");

    /// <summary>Números citados, na ordem em que aparecem pela primeira vez, sem repetir.</summary>
    public static List<int> Extrair(string texto)
    {
        var numeros = new List<int>();
        foreach (SegmentoResposta segmento in Segmentar(texto))
        {
            foreach (int numero in segmento.Numeros)
            {
                if (!numeros.Contains(numero))
                {
                    numeros.Add(numero);
                }
            }
        }

        return numeros;
    }

    /// <summary>Texto e citações intercalados, na ordem do texto. Juntos reproduzem o texto original.</summary>
    public static List<SegmentoResposta> Segmentar(string texto)
    {
        var segmentos = new List<SegmentoResposta>();
        int inicio = 0;

        foreach (Match citacao in RegexCitacao.Matches(texto))
        {
            if (citacao.Index > inicio)
            {
                segmentos.Add(new SegmentoResposta { Texto = texto[inicio..citacao.Index] });
            }

            var numeros = new List<int>();
            foreach (Match numero in Regex.Matches(citacao.Groups[1].Value, @"\d+"))
            {
                numeros.Add(int.Parse(numero.Value));
            }

            segmentos.Add(new SegmentoResposta { Texto = citacao.Value, Numeros = numeros });
            inicio = citacao.Index + citacao.Length;
        }

        if (inicio < texto.Length)
        {
            segmentos.Add(new SegmentoResposta { Texto = texto[inicio..] });
        }

        return segmentos;
    }
}
