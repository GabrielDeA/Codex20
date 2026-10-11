using Codex20.Contratos;
using Codex20.Core.Busca;
using Codex20.Core.Geracao;
using Codex20.Embeddings;
using Codex20.Geracao;

namespace Codex20.Api.Servicos;

/// <summary>
/// O RAG como o App o usa: sempre a busca híbrida (RRF) com 8 trechos, a melhor na avaliação.
/// Diferente do Painel, aqui não há escolha de estratégia nem "abre sem credenciais": sem elas a Api
/// não tem o que fazer, então falha já na subida.
/// </summary>
public class ServicoConsulta
{
    private readonly ArmazenamentoVetorialSqlite armazenamento;
    private readonly IndiceTextualFts5 indiceTextual;
    private readonly IEstrategiaBusca estrategia;
    private readonly PipelineRag pipeline;
    private readonly ILogger<ServicoConsulta> logger;

    private readonly int limiteDiario;
    private readonly object travaContador = new();
    private DateOnly diaContador;
    private int consultasNoDia;

    public ServicoConsulta(IConfiguration configuracao, ILogger<ServicoConsulta> logger)
    {
        this.logger = logger;

        armazenamento = new ArmazenamentoVetorialSqlite(CaminhosDados.Banco(), "chunks");
        // Mesmo arquivo: o índice léxico é outra virtual table ao lado da vec0.
        indiceTextual = new IndiceTextualFts5(CaminhosDados.Banco(), "chunks_fts");

        var cosseno = new EstrategiaBuscaCosseno(armazenamento, new GeradorEmbeddingAzureOpenAI(configuracao));
        var bm25 = new EstrategiaBuscaBm25(indiceTextual);
        estrategia = new EstrategiaBuscaRrf(new List<IEstrategiaBusca> { cosseno, bm25 });

        pipeline = new PipelineRag(new GeradorRespostaAzureOpenAI(configuracao));
        limiteDiario = configuracao.GetValue("Consulta:LimiteDiario", 200);
    }

    public string Modelo => pipeline.Modelo;

    public int LimiteDiario => limiteDiario;

    public async Task PrepararAsync()
    {
        await armazenamento.PrepararAsync();
        await indiceTextual.PrepararAsync();
    }

    /// <summary>
    /// Conta a consulta no limite do dia, que protege a assinatura do Azure: com a URL pública, cada
    /// pergunta de qualquer pessoa custa tokens. Fica em memória, então reiniciar a Api zera a conta.
    /// </summary>
    /// <returns><c>false</c> quando o limite do dia já foi atingido.</returns>
    public bool ReservarConsulta()
    {
        lock (travaContador)
        {
            DateOnly hoje = DateOnly.FromDateTime(DateTime.Now);
            if (hoje != diaContador)
            {
                diaContador = hoje;
                consultasNoDia = 0;
            }

            if (consultasNoDia >= limiteDiario)
            {
                return false;
            }

            consultasNoDia++;
            return true;
        }
    }

    public async Task<RespostaConsulta> ConsultarAsync(string pergunta)
    {
        RespostaGerada resposta = await pipeline.ResponderAsync(estrategia, new PedidoResposta { Pergunta = pergunta });

        // O que os testadores perguntam, e o que o RAG conseguiu responder, é material para o trabalho.
        logger.LogInformation(
            "Consulta \"{Pergunta}\": {Citadas} de {Trechos} trecho(s) citado(s), {TokensEntrada}+{TokensSaida} tokens, {Segundos:N1} s. {Observacao}",
            pergunta,
            resposta.Citacoes.Count,
            resposta.Trechos.Count,
            resposta.TokensEntrada,
            resposta.TokensSaida,
            (resposta.DuracaoBusca + resposta.DuracaoGeracao).TotalSeconds,
            resposta.Observacao ?? string.Empty);

        return ConversorResposta.Converter(resposta);
    }
}
