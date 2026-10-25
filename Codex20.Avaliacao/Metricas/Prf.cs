namespace Codex20.Avaliacao.Metricas;

/// <summary>Precisão, recall e F1 de um conjunto de citações contra o gabarito.</summary>
public class Prf
{
    public double Precisao { get; init; }

    public double Recall { get; init; }

    public double F1 { get; init; }

    /// <summary>Pelo menos uma citação bate com o gabarito.</summary>
    public bool IsAlgumaCerta { get; init; }

    /// <summary>A resposta não citou nada nesse nível (chunk, página ou livro).</summary>
    public bool IsSemCitacao { get; init; }

    /// <param name="citadosCertos">Citações que batem com alguma referência.</param>
    /// <param name="citados">Total de citações (distintas).</param>
    /// <param name="referenciasCobertas">Referências que alguma citação cobriu.</param>
    /// <param name="referencias">Total de referências.</param>
    /// <remarks>
    /// Sem citação nenhuma, a precisão é 0 e não indefinida: numa pergunta com resposta nos livros,
    /// não citar é não dar a fonte, e isso tem de pesar na média.
    /// </remarks>
    public static Prf Calcular(int citadosCertos, int citados, int referenciasCobertas, int referencias)
    {
        double precisao = citados == 0 ? 0 : (double)citadosCertos / citados;
        double recall = referencias == 0 ? 0 : (double)referenciasCobertas / referencias;
        double f1 = precisao + recall == 0 ? 0 : 2 * precisao * recall / (precisao + recall);

        return new Prf
        {
            Precisao = precisao,
            Recall = recall,
            F1 = f1,
            IsAlgumaCerta = citadosCertos > 0,
            IsSemCitacao = citados == 0,
        };
    }
}
