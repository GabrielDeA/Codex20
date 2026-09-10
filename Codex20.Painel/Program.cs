using Codex20.Painel.Components;
using Codex20.Painel.Servicos;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Uma instância por aba do navegador: guarda a última rodada de chunking enquanto se navega
// entre as páginas.
builder.Services.AddScoped<ServicoChunking>();

// Uma instância só: o cliente do Azure OpenAI e o banco vetorial não têm estado por aba.
builder.Services.AddSingleton<ServicoEmbeddings>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
