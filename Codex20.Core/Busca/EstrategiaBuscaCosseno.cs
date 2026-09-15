using Codex20.Core.Embeddings;

namespace Codex20.Core.Busca;

/// <summary>
/// Busca densa: embeda a consulta e pede ao vector store os vizinhos mais próximos por cosseno.
/// </summary>
public class EstrategiaBuscaCosseno : IEstrategiaBusca
{
    private readonly IArmazenamentoVetorial armazenamento;
    private readonly IGeradorEmbedding? gerador;

    /// <param name="gerador">
    /// Nulo quando faltam as credenciais: a estratégia fica indisponível, mas as que não dependem
    /// da API continuam funcionando.
    /// </param>
    public EstrategiaBuscaCosseno(IArmazenamentoVetorial armazenamento, IGeradorEmbedding? gerador)
    {
        this.armazenamento = armazenamento;
        this.gerador = gerador;
    }

    public string Nome => "cosseno";

    public string NomeExibicao => "Cosseno (vetorial)";

    public string Descricao =>
        "Embeda a consulta e devolve os chunks de vetor mais próximo pela similaridade de cosseno. "
        + "Acha paráfrases, mas não garante o termo exato. O tempo inclui a chamada à API de embeddings.";

    public string RotuloPontuacao => "Similaridade de cosseno (1 = idêntico)";

    public bool IsDisponivel => gerador is not null;

    public string? MotivoIndisponivel =>
        gerador is null ? "Precisa das credenciais do Azure OpenAI para embedar a consulta." : null;

    public async Task<RespostaBusca> BuscarAsync(PedidoBusca pedido)
    {
        if (gerador is null)
        {
            throw new InvalidOperationException(MotivoIndisponivel);
        }

        List<float[]> vetor = await gerador.GerarEmLoteAsync(new List<string> { pedido.Texto });
        List<ResultadoBusca> resultados = await armazenamento.BuscarSimilaresAsync(vetor[0], pedido.Quantidade);

        for (int i = 0; i < resultados.Count; i++)
        {
            resultados[i].Posicao = i + 1;
        }

        return new RespostaBusca
        {
            NomeEstrategia = Nome,
            RotuloPontuacao = RotuloPontuacao,
            Resultados = resultados,
        };
    }
}
