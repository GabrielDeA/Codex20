using Azure;
using Azure.AI.DocumentIntelligence;
using Microsoft.Extensions.Configuration;
using System.ClientModel.Primitives;

namespace Codex20.Ingestion;

public class DocumentIntelligenceService(IConfiguration configuration)
{
    public async Task LerDocumentoAsync(string caminhoArquivo, string diretorioDados)
    {
        string endpoint = configuration["AzureDocumentIntelligence:Endpoint"]
            ?? throw new InvalidOperationException(
                "Configure 'AzureDocumentIntelligence:Endpoint' via 'dotnet user-secrets set' (veja README) ou a variável de ambiente AzureDocumentIntelligence__Endpoint.");

        string key = configuration["AzureDocumentIntelligence:Key"]
            ?? throw new InvalidOperationException(
                "Configure 'AzureDocumentIntelligence:Key' via 'dotnet user-secrets set' (veja README) ou a variável de ambiente AzureDocumentIntelligence__Key.");

        var cliente = new DocumentIntelligenceClient(new Uri(endpoint), new AzureKeyCredential(key));

        using var stream = File.OpenRead(caminhoArquivo);
        BinaryData bytes = BinaryData.FromStream(stream);

        var options = new AnalyzeDocumentOptions("prebuilt-layout", bytes)
        {
            OutputContentFormat = DocumentContentFormat.Markdown
        };

        Operation<AnalyzeResult> operation = await cliente.AnalyzeDocumentAsync(WaitUntil.Completed, options);

        AnalyzeResult result = operation.Value;
        string resultText = result.Content ?? string.Empty;

        string nomeArquivo = Path.GetFileNameWithoutExtension(caminhoArquivo);

        string diretorioMarkdown = Path.Combine(diretorioDados, "Markdown");
        string diretorioJson = Path.Combine(diretorioDados, "Json");
        Directory.CreateDirectory(diretorioMarkdown);
        Directory.CreateDirectory(diretorioJson);

        string caminhoResultado = Path.Combine(diretorioMarkdown, $"{nomeArquivo}.md");
        File.WriteAllText(caminhoResultado, resultText);

        string caminhoResultadoJson = Path.Combine(diretorioJson, $"{nomeArquivo}.json");
        SalvarResultadoJson(result, caminhoResultadoJson);

        Console.WriteLine("Documento processado com sucesso!");
        Console.WriteLine($"Resultado salvo em: {caminhoResultado}");
        Console.WriteLine($"Resultado (JSON) salvo em: {caminhoResultadoJson}");
    }

    private static void SalvarResultadoJson(AnalyzeResult result, string caminhoArquivo)
    {
        BinaryData json = ModelReaderWriter.Write(result);
        File.WriteAllText(caminhoArquivo, json.ToString());
    }
}
