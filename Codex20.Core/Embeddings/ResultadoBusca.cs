using Codex20.Core.Busca;
using Codex20.Core.Chunking;

namespace Codex20.Core.Embeddings;

/// <summary>
/// Um chunk devolvido por uma busca, com o quanto ele casou com a consulta
/// </summary>
public class ResultadoBusca
{
    public Chunk Chunk { get; init; } = new();

    /// <summary>
    /// Convenção única entre as estratégias: maior = melhor. A escala, porém, é de cada uma
    /// (cosseno 0–1; BM25 relativo ao corpus; RRF na casa de 0,0x), por isso o número só faz
    /// sentido ao lado do <see cref="RespostaBusca.RotuloPontuacao"/> — comparar pontuações entre
    /// estratégias diferentes não significa nada.
    /// </summary>
    public double Pontuacao { get; init; }

    /// <summary>
    /// Colocação no ranking da estratégia, começando em 1
    /// </summary>
    public int Posicao { get; set; }

    /// <summary>
    /// De onde veio a colocação quando a estratégia funde outras (RRF). Vazia nas demais.
    /// </summary>
    public List<ContribuicaoEstrategia> Contribuicoes { get; init; } = new();
}
