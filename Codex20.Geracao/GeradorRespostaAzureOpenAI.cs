using System.ClientModel;
using System.Text;
using Codex20.Core.Geracao;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;

namespace Codex20.Geracao;

/// <summary>
/// Gera a resposta chamando um deployment de chat do Microsoft Foundry pela API v1 da Azure
/// (<c>/openai/v1/</c>). É Chat Completions de propósito: é a API que os modelos de terceiros do
/// Foundry (DeepSeek, Kimi) também atendem, então trocar de modelo é só trocar o deployment.
/// </summary>
public class GeradorRespostaAzureOpenAI : IGeradorResposta
{
    /// <summary>
    /// Teto de segurança, não tamanho esperado. Em modelo de raciocínio ele conta também os tokens
    /// de pensamento: apertado demais, o modelo gasta tudo pensando e devolve texto vazio.
    /// </summary>
    private const int MaximoTokensSaida = 4096;

    private readonly ChatClient cliente;
    private readonly string deployment;
    private readonly string? esforcoRaciocinio;

    public GeradorRespostaAzureOpenAI(IConfiguration configuration)
    {
        string endpoint = configuration["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException(
                "Configure 'AzureOpenAI:Endpoint' via 'dotnet user-secrets set' ou a variável de ambiente AzureOpenAI__Endpoint.");

        string key = configuration["AzureOpenAI:Key"]
            ?? throw new InvalidOperationException(
                "Configure 'AzureOpenAI:Key' via 'dotnet user-secrets set' ou a variável de ambiente AzureOpenAI__Key.");

        deployment = configuration["AzureOpenAI:DeploymentChat"]
            ?? throw new InvalidOperationException(
                "Configure 'AzureOpenAI:DeploymentChat' (nome do deployment de chat, ex. gpt-5.6-luna) via 'dotnet user-secrets set' ou a variável de ambiente AzureOpenAI__DeploymentChat.");

        // Opcional: só modelo de raciocínio aceita o parâmetro, e mandá-lo a um que não aceita dá erro.
        esforcoRaciocinio = configuration["AzureOpenAI:EsforcoRaciocinio"];
        if (string.IsNullOrWhiteSpace(esforcoRaciocinio))
        {
            esforcoRaciocinio = null;
        }

        var opcoes = new OpenAIClientOptions { Endpoint = new Uri(new Uri(endpoint), "openai/v1/") };
        cliente = new OpenAIClient(new ApiKeyCredential(key), opcoes).GetChatClient(deployment);
    }

    public string Modelo => esforcoRaciocinio is null ? deployment : $"{deployment} (raciocínio {esforcoRaciocinio})";

    public async Task<ConclusaoModelo> GerarAsync(string instrucoes, string mensagem)
    {
        var mensagens = new List<ChatMessage>
        {
            new SystemChatMessage(instrucoes),
            new UserChatMessage(mensagem),
        };

        // Sem temperature: os modelos de raciocínio (família GPT-5) só aceitam o valor padrão.
        var opcoes = new ChatCompletionOptions { MaxOutputTokenCount = MaximoTokensSaida };
        if (esforcoRaciocinio is not null)
        {
            opcoes.ReasoningEffortLevel = new ChatReasoningEffortLevel(esforcoRaciocinio);
        }

        ClientResult<ChatCompletion> resultado = await cliente.CompleteChatAsync(mensagens, opcoes);
        ChatCompletion conclusao = resultado.Value;

        var texto = new StringBuilder();
        foreach (ChatMessageContentPart parte in conclusao.Content)
        {
            texto.Append(parte.Text);
        }

        int tokensRaciocinio = 0;
        if (conclusao.Usage?.OutputTokenDetails is not null)
        {
            tokensRaciocinio = conclusao.Usage.OutputTokenDetails.ReasoningTokenCount;
        }

        return new ConclusaoModelo
        {
            Texto = texto.ToString().Trim(),
            TokensEntrada = conclusao.Usage?.InputTokenCount ?? 0,
            TokensSaida = conclusao.Usage?.OutputTokenCount ?? 0,
            TokensRaciocinio = tokensRaciocinio,
            IsCortada = conclusao.FinishReason == ChatFinishReason.Length,
        };
    }
}
