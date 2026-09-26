using System.ClientModel;
using Codex20.Core.Embeddings;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Embeddings;

namespace Codex20.Embeddings;

/// <summary>
/// Gera embeddings chamando um deployment do Azure OpenAI. Uma chamada por lote recebido —
/// quem divide o corpus em lotes é o chamador.
/// </summary>
/// <remarks>
/// Fala com a API v1 da Azure (<c>/openai/v1/</c>) pelo SDK da OpenAI, a mesma porta usada pelo
/// gerador de respostas do Codex20.Geracao. O "modelo" que se passa ao SDK é o nome do deployment.
/// </remarks>
public class GeradorEmbeddingAzureOpenAI : IGeradorEmbedding
{
    private readonly EmbeddingClient cliente;
    private readonly string deployment;

    public GeradorEmbeddingAzureOpenAI(IConfiguration configuration)
    {
        string endpoint = configuration["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException(
                "Configure 'AzureOpenAI:Endpoint' via 'dotnet user-secrets set' ou a variável de ambiente AzureOpenAI__Endpoint.");

        string key = configuration["AzureOpenAI:Key"]
            ?? throw new InvalidOperationException(
                "Configure 'AzureOpenAI:Key' via 'dotnet user-secrets set' ou a variável de ambiente AzureOpenAI__Key.");

        deployment = configuration["AzureOpenAI:DeploymentEmbedding"]
            ?? throw new InvalidOperationException(
                "Configure 'AzureOpenAI:DeploymentEmbedding' (nome do deployment, ex. text-embedding-3-large) via 'dotnet user-secrets set' ou a variável de ambiente AzureOpenAI__DeploymentEmbedding.");

        var opcoes = new OpenAIClientOptions { Endpoint = new Uri(new Uri(endpoint), "openai/v1/") };
        cliente = new OpenAIClient(new ApiKeyCredential(key), opcoes).GetEmbeddingClient(deployment);
    }

    public string Modelo => deployment;

    public int Dimensoes => ArmazenamentoVetorialSqlite.Dimensoes;

    public async Task<List<float[]>> GerarEmLoteAsync(List<string> textos)
    {
        ClientResult<OpenAIEmbeddingCollection> resposta = await cliente.GenerateEmbeddingsAsync(textos);

        var vetores = new List<float[]>();
        foreach (OpenAIEmbedding embedding in resposta.Value)
        {
            float[] vetor = embedding.ToFloats().ToArray();
            if (vetor.Length != Dimensoes)
            {
                throw new InvalidOperationException(
                    $"O deployment '{deployment}' devolveu vetor de {vetor.Length} dimensões, mas o vector store espera {Dimensoes}. "
                    + $"Use um modelo de {Dimensoes} dimensões ou ajuste {nameof(ArmazenamentoVetorialSqlite)}.{nameof(ArmazenamentoVetorialSqlite.Dimensoes)}.");
            }

            vetores.Add(vetor);
        }

        return vetores;
    }
}
