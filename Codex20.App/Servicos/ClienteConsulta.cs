using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex20.Contratos;
using Microsoft.JSInterop;

namespace Codex20.App.Servicos;

/// <summary>
/// Fala com a Api e guarda a chave de acesso. A chave chega no link de convite (?chave=...) ou é
/// digitada na tela, e fica no localStorage para a pessoa não precisar do link de novo.
/// </summary>
public class ClienteConsulta
{
    /// <summary>Mesmo nome que o ConsultasController da Api lê.</summary>
    private const string HeaderChaveAcesso = "X-Chave-Acesso";

    private const string ItemArmazenamento = "codex20.chaveAcesso";

    private readonly HttpClient http;
    private readonly IJSRuntime js;
    private string? chaveAcesso;

    public ClienteConsulta(HttpClient http, IJSRuntime js)
    {
        this.http = http;
        this.js = js;
    }

    public bool IsComChave => !string.IsNullOrWhiteSpace(chaveAcesso);

    public async Task CarregarChaveAsync()
    {
        chaveAcesso = await js.InvokeAsync<string?>("localStorage.getItem", ItemArmazenamento);
    }

    public async Task SalvarChaveAsync(string chave)
    {
        chaveAcesso = chave.Trim();
        await js.InvokeVoidAsync("localStorage.setItem", ItemArmazenamento, chaveAcesso);
    }

    public async Task EsquecerChaveAsync()
    {
        chaveAcesso = null;
        await js.InvokeVoidAsync("localStorage.removeItem", ItemArmazenamento);
    }

    public async Task<RespostaConsulta> ConsultarAsync(string pergunta)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Post, "api/consultas")
        {
            Content = JsonContent.Create(new PedidoConsulta { Pergunta = pergunta }),
        };
        requisicao.Headers.Add(HeaderChaveAcesso, chaveAcesso);
        // O plano gratuito do ngrok põe uma página de aviso na frente de requisições de navegador;
        // este header pede para pular. Fora do ngrok ele é simplesmente ignorado.
        requisicao.Headers.Add("ngrok-skip-browser-warning", "1");

        HttpResponseMessage resposta;
        try
        {
            resposta = await http.SendAsync(requisicao);
        }
        catch (HttpRequestException)
        {
            throw new ErroConsulta("Sem conexão com o servidor. Confira sua internet ou tente mais tarde.", false);
        }

        if (resposta.IsSuccessStatusCode)
        {
            RespostaConsulta? conteudo = await resposta.Content.ReadFromJsonAsync<RespostaConsulta>();
            if (conteudo is null)
            {
                throw new ErroConsulta("O servidor devolveu uma resposta vazia.", false);
            }

            return conteudo;
        }

        bool isAcessoNegado = resposta.StatusCode == HttpStatusCode.Unauthorized;
        throw new ErroConsulta(await LerMensagemErroAsync(resposta), isAcessoNegado);
    }

    /// <summary>
    /// A Api explica o erro no campo "detail" (ProblemDetails). O rate limiter, que recusa antes de
    /// chegar no controller, devolve só o 429 sem corpo — daí as mensagens por status.
    /// </summary>
    private static async Task<string> LerMensagemErroAsync(HttpResponseMessage resposta)
    {
        try
        {
            ProblemaHttp? problema = await resposta.Content.ReadFromJsonAsync<ProblemaHttp>();
            if (problema is not null && !string.IsNullOrWhiteSpace(problema.Detail))
            {
                return problema.Detail;
            }
        }
        catch (JsonException)
        {
            // Corpo vazio ou que não é JSON: cai na mensagem pelo status.
        }

        if (resposta.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return "Muitas perguntas seguidas. Espere um minuto e tente de novo.";
        }

        if (resposta.StatusCode == HttpStatusCode.Unauthorized)
        {
            return "Acesso restrito: abra o app pelo link que você recebeu.";
        }

        return "Não foi possível responder agora. Tente de novo em instantes.";
    }

    private class ProblemaHttp
    {
        public string? Detail { get; set; }
    }
}
