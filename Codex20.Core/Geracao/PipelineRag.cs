using System.Diagnostics;
using Codex20.Core.Busca;

namespace Codex20.Core.Geracao;

/// <summary>
/// O RAG de ponta a ponta: busca os trechos, monta o prompt, chama o modelo e confere as citações.
/// Só depende de interfaces — a estratégia de busca e o modelo são escolhidos por quem chama.
/// </summary>
public class PipelineRag
{
    private readonly IGeradorResposta gerador;

    public PipelineRag(IGeradorResposta gerador)
    {
        this.gerador = gerador;
    }

    public string Modelo => gerador.Modelo;

    /// <param name="busca">
    /// Por chamada, e não no construtor, para a tela poder comparar a resposta com cada estratégia.
    /// </param>
    public async Task<RespostaGerada> ResponderAsync(IEstrategiaBusca busca, PedidoResposta pedido)
    {
        var relogio = Stopwatch.StartNew();
        RespostaBusca resultadoBusca = await busca.BuscarAsync(new PedidoBusca
        {
            Texto = pedido.Pergunta,
            Quantidade = pedido.QuantidadeTrechos,
        });
        TimeSpan duracaoBusca = relogio.Elapsed;

        // Sem trecho nenhum o modelo só teria o próprio conhecimento para responder — exatamente o
        // que as instruções proíbem. Melhor nem chamar (e nem pagar).
        if (resultadoBusca.Resultados.Count == 0)
        {
            string motivo = "A busca não trouxe nenhum trecho; o modelo não foi chamado.";
            if (resultadoBusca.Observacao is not null)
            {
                motivo += " " + resultadoBusca.Observacao;
            }

            return new RespostaGerada
            {
                Pergunta = pedido.Pergunta,
                Modelo = gerador.Modelo,
                NomeEstrategiaBusca = busca.Nome,
                DuracaoBusca = duracaoBusca,
                Observacao = motivo,
            };
        }

        string mensagem = MontadorPrompt.MontarMensagem(pedido.Pergunta, resultadoBusca.Resultados);

        relogio.Restart();
        ConclusaoModelo conclusao = await gerador.GerarAsync(MontadorPrompt.Instrucoes, mensagem);
        TimeSpan duracaoGeracao = relogio.Elapsed;

        var citacoes = new List<int>();
        var inexistentes = new List<int>();
        foreach (int numero in LeitorCitacoes.Extrair(conclusao.Texto))
        {
            if (numero >= 1 && numero <= resultadoBusca.Resultados.Count)
            {
                citacoes.Add(numero);
            }
            else
            {
                inexistentes.Add(numero);
            }
        }

        return new RespostaGerada
        {
            Pergunta = pedido.Pergunta,
            Texto = conclusao.Texto,
            Modelo = gerador.Modelo,
            NomeEstrategiaBusca = busca.Nome,
            Trechos = resultadoBusca.Resultados,
            Citacoes = citacoes,
            CitacoesInexistentes = inexistentes,
            Mensagem = mensagem,
            TokensEntrada = conclusao.TokensEntrada,
            TokensSaida = conclusao.TokensSaida,
            TokensRaciocinio = conclusao.TokensRaciocinio,
            DuracaoBusca = duracaoBusca,
            DuracaoGeracao = duracaoGeracao,
            IsCortada = conclusao.IsCortada,
            Observacao = MontarObservacao(conclusao, inexistentes, resultadoBusca.Observacao),
        };
    }

    private static string? MontarObservacao(ConclusaoModelo conclusao, List<int> inexistentes, string? observacaoBusca)
    {
        var avisos = new List<string>();

        if (conclusao.IsCortada)
        {
            avisos.Add("O modelo bateu no limite de tokens de saída e a resposta pode estar incompleta.");
        }

        if (inexistentes.Count > 0)
        {
            avisos.Add($"O modelo citou trecho(s) que não estavam no prompt: [{string.Join("], [", inexistentes)}].");
        }

        if (observacaoBusca is not null)
        {
            avisos.Add("Busca: " + observacaoBusca);
        }

        return avisos.Count == 0 ? null : string.Join(" ", avisos);
    }
}
