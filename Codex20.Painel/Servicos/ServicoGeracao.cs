using Codex20.Core.Busca;
using Codex20.Core.Geracao;
using Codex20.Geracao;

namespace Codex20.Painel.Servicos;

/// <summary>
/// Liga o <see cref="PipelineRag"/> às estratégias do <see cref="ServicoBusca"/>. Sem o deployment
/// de chat configurado o Painel abre igual; só a tela Perguntar fica bloqueada, com a mensagem de
/// <see cref="ErroConfiguracao"/>.
/// </summary>
public class ServicoGeracao
{
    private readonly ServicoEmbeddings servicoEmbeddings;
    private readonly ServicoBusca servicoBusca;
    private readonly PipelineRag? pipeline;

    public ServicoGeracao(IConfiguration configuracao, ServicoEmbeddings servicoEmbeddings, ServicoBusca servicoBusca)
    {
        this.servicoEmbeddings = servicoEmbeddings;
        this.servicoBusca = servicoBusca;

        try
        {
            pipeline = new PipelineRag(new GeradorRespostaAzureOpenAI(configuracao));
        }
        catch (InvalidOperationException erro)
        {
            ErroConfiguracao = erro.Message;
        }
    }

    public string? ErroConfiguracao { get; }

    public bool IsConfigurado => pipeline is not null;

    public string Modelo => pipeline is null ? "(não configurado)" : pipeline.Modelo;

    public List<IEstrategiaBusca> Estrategias => servicoBusca.Estrategias;

    public IEstrategiaBusca ObterEstrategia(string nome)
    {
        return servicoBusca.Obter(nome);
    }

    public async Task<RespostaGerada> ResponderAsync(string nomeEstrategia, PedidoResposta pedido)
    {
        if (pipeline is null)
        {
            throw new InvalidOperationException(ErroConfiguracao);
        }

        IEstrategiaBusca estrategia = servicoBusca.Obter(nomeEstrategia);
        if (!estrategia.IsDisponivel)
        {
            throw new InvalidOperationException(estrategia.MotivoIndisponivel);
        }

        await servicoEmbeddings.PrepararAsync();
        return await pipeline.ResponderAsync(estrategia, pedido);
    }
}
