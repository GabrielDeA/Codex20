using Codex20.Core.Embeddings;

namespace Codex20.Core.Busca;

/// <summary>
/// Reciprocal Rank Fusion (Cormack, Clarke &amp; Buettcher, SIGIR 2009): soma 1/(k + posição) de
/// cada perna. Como só a colocação entra na conta, as escalas incomparáveis das pernas (cosseno
/// de 0 a 1, BM25 sem limite) deixam de ser problema — não é preciso normalizar nada.
/// </summary>
public class EstrategiaBuscaRrf : IEstrategiaBusca
{
    /// <summary>
    /// k do artigo original, que virou default de fato (Elasticsearch, Azure AI Search). Ele achata
    /// a curva: da 1ª para a 10ª colocação o peso cai só de 0,0164 para 0,0143, e é isso que faz a
    /// fusão premiar concordância — um chunk decente nas duas listas passa na frente de um que é 1º
    /// numa só. Com k pequeno, a 1ª colocação dominaria e a fusão viraria um "ou" entre as pernas.
    /// </summary>
    private const int KRrf = 60;

    /// <summary>
    /// Fundir só o top-N final degeneraria a fusão: um chunk que é 8º numa perna e 1º na outra
    /// precisa estar nas duas listas para somar. 50 é janela larga perto dos ~3 mil chunks e sai
    /// barato — o vec0 varre tudo de qualquer jeito e o FTS5 com LIMIT 50 também é rápido.
    /// </summary>
    private const int CandidatosMinimos = 50;

    private readonly List<IEstrategiaBusca> pernas;

    /// <param name="pernas">
    /// Prontas, montadas por quem cria a fusão. Não peça "todas as estratégias" a um contêiner de
    /// DI: a própria fusão viria na lista, como perna de si mesma.
    /// </param>
    public EstrategiaBuscaRrf(List<IEstrategiaBusca> pernas)
    {
        this.pernas = pernas;
    }

    public string Nome => "rrf";

    public string NomeExibicao => "RRF (híbrida)";

    public string Descricao =>
        $"Reciprocal Rank Fusion de {NomesDasPernas()}: soma 1/({KRrf} + posição) de cada ranking, "
        + "então sobe quem vai bem em todos, sem comparar escalas. O tempo inclui rodar as pernas.";

    public string RotuloPontuacao => $"RRF (k={KRrf}; maior = melhor)";

    /// <summary>
    /// Todas as pernas, ou nada: um número rotulado "RRF" calculado sobre uma lista só seria
    /// mentira na tabela comparativa.
    /// </summary>
    public bool IsDisponivel
    {
        get
        {
            foreach (IEstrategiaBusca perna in pernas)
            {
                if (!perna.IsDisponivel)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public string? MotivoIndisponivel
    {
        get
        {
            var motivos = new List<string>();
            foreach (IEstrategiaBusca perna in pernas)
            {
                if (!perna.IsDisponivel)
                {
                    motivos.Add($"{perna.NomeExibicao}: {perna.MotivoIndisponivel}");
                }
            }

            return motivos.Count == 0 ? null : string.Join(" ", motivos);
        }
    }

    public async Task<RespostaBusca> BuscarAsync(PedidoBusca pedido)
    {
        var pedidoPerna = new PedidoBusca
        {
            Texto = pedido.Texto,
            Quantidade = Math.Max(pedido.Quantidade, CandidatosMinimos),
        };

        // A chave da fusão é o Chunk.Id, derivado de Livro + Texto. As pernas chegam ao mesmo Id
        // porque o texto volta do banco byte a byte como entrou: normalizar o texto (trim, espaços)
        // em só um dos caminhos de gravação faria a fusão parar de casar chunks, sem erro nenhum.
        var pontuacaoPorId = new Dictionary<Guid, double>();
        var chunkPorId = new Dictionary<Guid, ResultadoBusca>();
        var posicoesPorPerna = new List<Dictionary<Guid, ResultadoBusca>>();
        var observacoes = new List<string>();

        foreach (IEstrategiaBusca perna in pernas)
        {
            RespostaBusca resposta = await perna.BuscarAsync(pedidoPerna);
            if (resposta.Observacao is not null)
            {
                observacoes.Add($"{perna.NomeExibicao}: {resposta.Observacao}");
            }

            var posicoes = new Dictionary<Guid, ResultadoBusca>();
            for (int i = 0; i < resposta.Resultados.Count; i++)
            {
                ResultadoBusca resultado = resposta.Resultados[i];
                Guid id = resultado.Chunk.Id;
                if (posicoes.ContainsKey(id))
                {
                    continue;
                }

                resultado.Posicao = i + 1;
                posicoes[id] = resultado;
                pontuacaoPorId[id] = pontuacaoPorId.GetValueOrDefault(id) + 1.0 / (KRrf + i + 1);
                chunkPorId.TryAdd(id, resultado);
            }

            posicoesPorPerna.Add(posicoes);
        }

        var ids = new List<Guid>(pontuacaoPorId.Keys);
        ids.Sort((a, b) => pontuacaoPorId[b].CompareTo(pontuacaoPorId[a]));

        var fundidos = new List<ResultadoBusca>();
        for (int i = 0; i < ids.Count && i < pedido.Quantidade; i++)
        {
            Guid id = ids[i];
            var contribuicoes = new List<ContribuicaoEstrategia>();

            for (int p = 0; p < pernas.Count; p++)
            {
                // Posição 0 marca a perna que não trouxe o chunk — a tela mostra "—".
                bool isPresente = posicoesPorPerna[p].TryGetValue(id, out ResultadoBusca? naPerna);
                contribuicoes.Add(new ContribuicaoEstrategia
                {
                    NomeEstrategia = pernas[p].Nome,
                    Posicao = isPresente ? naPerna!.Posicao : 0,
                    Pontuacao = isPresente ? naPerna!.Pontuacao : 0,
                });
            }

            fundidos.Add(new ResultadoBusca
            {
                Chunk = chunkPorId[id].Chunk,
                Pontuacao = pontuacaoPorId[id],
                Posicao = i + 1,
                Contribuicoes = contribuicoes,
            });
        }

        return new RespostaBusca
        {
            NomeEstrategia = Nome,
            RotuloPontuacao = RotuloPontuacao,
            Resultados = fundidos,
            Observacao = observacoes.Count == 0 ? null : string.Join(" ", observacoes),
        };
    }

    private string NomesDasPernas()
    {
        var nomes = new List<string>();
        foreach (IEstrategiaBusca perna in pernas)
        {
            nomes.Add(perna.Nome);
        }

        return string.Join(" + ", nomes);
    }
}
