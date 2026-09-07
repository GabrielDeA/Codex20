using System.ClientModel;
using Azure.AI.OpenAI;
using Codex20.Core.Embeddings;
using Microsoft.Extensions.Configuration;
using OpenAI.Embeddings;

namespace Codex20.Embeddings;

/// <summary>
/// Gera embeddings chamando um deployment do Azure OpenAI. Uma chamada por lote recebido —
/// quem divide o corpus em lotes é o chamador.
/// </summary>
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

        var azure = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(key));
        cliente = azure.GetEmbeddingClient(deployment);
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
