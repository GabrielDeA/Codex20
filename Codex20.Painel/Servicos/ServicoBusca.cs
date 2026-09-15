using System.Diagnostics;
using Codex20.Core.Busca;

namespace Codex20.Painel.Servicos;

/// <summary>
/// Catálogo das estratégias de busca e ponto único de execução. É aqui que o tempo é medido, para
/// que todas as colunas da comparação sejam cronometradas do mesmo jeito.
/// </summary>
public class ServicoBusca
{
    private readonly ServicoEmbeddings servicoEmbeddings;
    private readonly List<IEstrategiaBusca> estrategias;

    public ServicoBusca(ServicoEmbeddings servicoEmbeddings)
    {
        this.servicoEmbeddings = servicoEmbeddings;
        estrategias = MontarEstrategias(servicoEmbeddings);
    }

    /// <summary>Na ordem em que aparecem no select e nas colunas da comparação.</summary>
    public List<IEstrategiaBusca> Estrategias => estrategias;

    public IEstrategiaBusca Obter(string nome)
    {
        foreach (IEstrategiaBusca estrategia in estrategias)
        {
            if (estrategia.Nome == nome)
            {
                return estrategia;
            }
        }

        throw new ArgumentException($"Estratégia desconhecida: {nome}");
    }

    public async Task<RespostaBusca> ExecutarAsync(string nome, PedidoBusca pedido)
    {
        return await ExecutarAsync(Obter(nome), pedido);
    }

    /// <summary>
    /// Roda todas sobre a mesma consulta, uma de cada vez: em paralelo elas disputariam CPU e o
    /// arquivo do banco, e os tempos não significariam nada.
    /// </summary>
    public async Task<List<RespostaBusca>> CompararAsync(PedidoBusca pedido)
    {
        var respostas = new List<RespostaBusca>();
        foreach (IEstrategiaBusca estrategia in estrategias)
        {
            respostas.Add(await ExecutarAsync(estrategia, pedido));
        }

        return respostas;
    }

    /// <summary>
    /// Uma 4ª estratégia entra aqui. Se for outra fusão, passe as pernas prontas, como no RRF.
    /// </summary>
    private static List<IEstrategiaBusca> MontarEstrategias(ServicoEmbeddings servico)
    {
        var cosseno = new EstrategiaBuscaCosseno(servico.Armazenamento, servico.Gerador);
        var bm25 = new EstrategiaBuscaBm25(servico.IndiceTextual);
        var rrf = new EstrategiaBuscaRrf(new List<IEstrategiaBusca> { cosseno, bm25 });

        return new List<IEstrategiaBusca> { cosseno, bm25, rrf };
    }

    private async Task<RespostaBusca> ExecutarAsync(IEstrategiaBusca estrategia, PedidoBusca pedido)
    {
        if (!estrategia.IsDisponivel)
        {
            return new RespostaBusca
            {
                NomeEstrategia = estrategia.Nome,
                RotuloPontuacao = estrategia.RotuloPontuacao,
                Observacao = estrategia.MotivoIndisponivel,
            };
        }

        await servicoEmbeddings.PrepararAsync();
        var relogio = Stopwatch.StartNew();

        RespostaBusca resposta;
        try
        {
            resposta = await estrategia.BuscarAsync(pedido);
        }
        catch (Exception erro)
        {
            // Na comparação, uma estratégia que falha vira coluna com o motivo, sem derrubar as outras.
            resposta = new RespostaBusca
            {
                NomeEstrategia = estrategia.Nome,
                RotuloPontuacao = estrategia.RotuloPontuacao,
                Observacao = $"Erro: {erro.Message}",
            };
        }

        resposta.Duracao = relogio.Elapsed;
        return resposta;
    }
}
