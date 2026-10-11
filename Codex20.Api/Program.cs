using System.Threading.RateLimiting;
using Codex20.Api.Servicos;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// O ASP.NET só lê os user-secrets em Development. A versão publicada (Release, a que vai para os
// testadores) roda como Production, mas na mesma máquina e com as mesmas credenciais.
if (!builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

// Sem chave, a Api ficaria aberta a quem descobrisse a URL do ngrok, gastando tokens da assinatura.
if (string.IsNullOrWhiteSpace(builder.Configuration["Consulta:ChaveAcesso"]))
{
    throw new InvalidOperationException(
        "Configure 'Consulta:ChaveAcesso' via 'dotnet user-secrets set' no projeto Codex20.Api. " +
        "É a chave que vai no link de convite: https://<endereço>/?chave=<chave>");
}

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Uma instância só: o banco e os clientes do Azure OpenAI não guardam estado por consulta.
builder.Services.AddSingleton<ServicoConsulta>();

// Atrás do ngrok toda requisição chega de 127.0.0.1; o IP de quem perguntou vem no
// X-Forwarded-For. Sem isto o limite por IP viraria um limite só, compartilhado por todo mundo.
builder.Services.Configure<ForwardedHeadersOptions>(opcoes =>
{
    opcoes.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

int limitePorMinuto = builder.Configuration.GetValue("Consulta:LimitePorMinuto", 5);
builder.Services.AddRateLimiter(opcoes =>
{
    opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opcoes.AddPolicy(PoliticasLimite.PorIp, contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = limitePorMinuto, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

// Monta o serviço já na subida: credencial faltando aparece agora, e não na primeira pergunta.
ServicoConsulta servicoConsulta = app.Services.GetRequiredService<ServicoConsulta>();
await servicoConsulta.PrepararAsync();
app.Logger.LogInformation("Modelo de resposta: {Modelo}. Limite: {Limite} consultas por dia.", servicoConsulta.Modelo, servicoConsulta.LimiteDiario);

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseWebAssemblyDebugging();
}

// Sem UseHttpsRedirection: para fora quem entrega HTTPS é o ngrok.
// O MapStaticAssets já serve os arquivos do App, inclusive o _framework do WebAssembly.
app.MapStaticAssets();

app.UseRateLimiter();
app.MapControllers();

// Toda rota que não é da Api é do App. O index.html vai com no-cache: sem isso o navegador guarda
// a página por conta própria e, depois de uma atualização, continua pedindo arquivos que já não
// existem e trava no carregamento.
var opcoesIndex = new StaticFileOptions
{
    OnPrepareResponse = contexto => contexto.Context.Response.Headers.CacheControl = "no-cache",
};
app.MapFallbackToFile("index.html", opcoesIndex);

app.Run();
