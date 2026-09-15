using Codex20.Core.Embeddings;

namespace Codex20.Core.Busca;

/// <summary>
/// Busca léxica: ranqueia pelo BM25 do índice textual. Não depende da API de embeddings.
/// </summary>
public class EstrategiaBuscaBm25 : IEstrategiaBusca
{
    private readonly IIndiceTextual indice;

    public EstrategiaBuscaBm25(IIndiceTextual indice)
    {
        this.indice = indice;
    }

    public string Nome => "bm25";

    public string NomeExibicao => "BM25 (palavra-chave)";

    public string Descricao =>
        "Ranqueamento léxico do FTS5: pesa quantas vezes cada termo aparece no chunk contra a raridade dele no corpus. "
        + "Acha o termo exato, mas não entende sinônimos nem flexões. Não usa a API.";

    public string RotuloPontuacao => "BM25 (relativo ao corpus; maior = melhor)";

    public bool IsDisponivel => true;

    public string? MotivoIndisponivel => null;

    public async Task<RespostaBusca> BuscarAsync(PedidoBusca pedido)
    {
        var resposta = new RespostaBusca
        {
            NomeEstrategia = Nome,
            RotuloPontuacao = RotuloPontuacao,
        };

        if (!IsPesquisavel(pedido.Texto))
        {
            resposta.Observacao = "A consulta não tem nenhum termo pesquisável.";
            return resposta;
        }

        List<ResultadoBusca> resultados = await indice.BuscarTextoAsync(pedido.Texto, pedido.Quantidade);

        for (int i = 0; i < resultados.Count; i++)
        {
            resultados[i].Posicao = i + 1;
            resposta.Resultados.Add(resultados[i]);
        }

        if (resultados.Count == 0)
        {
            resposta.Observacao = "Nenhum chunk contém os termos da consulta. "
                + "Se o índice ainda não foi construído, use “Indexar BM25” na tela Embeddings.";
        }

        return resposta;
    }

    private static bool IsPesquisavel(string texto)
    {
        foreach (char c in texto)
        {
            if (char.IsLetterOrDigit(c))
            {
                return true;
            }
        }

        return false;
    }
}
