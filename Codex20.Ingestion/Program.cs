using System.Reflection;
using Codex20.Ingestion;
using Microsoft.Extensions.Configuration;

if (args.Length == 0)
{
    Console.WriteLine("Uso: Codex20.Ingestion <caminho-do-pdf>");
    return 1;
}

IConfiguration configuration = new ConfigurationBuilder()
    .AddUserSecrets(Assembly.GetExecutingAssembly())
    .AddEnvironmentVariables()
    .Build();

string caminhoArquivo = args[0];
string diretorioDados = Path.Combine(Directory.GetCurrentDirectory(), "Data");

var docService = new DocumentIntelligenceService(configuration);
await docService.LerDocumentoAsync(caminhoArquivo, diretorioDados);

return 0;
