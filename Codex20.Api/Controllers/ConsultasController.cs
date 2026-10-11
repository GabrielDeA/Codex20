using System.Security.Cryptography;
using System.Text;
using Codex20.Api.Servicos;
using Codex20.Contratos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Codex20.Api.Controllers;

[ApiController]
[Route("api/consultas")]
public class ConsultasController : ControllerBase
{
    /// <summary>O App manda neste header a chave que veio no link de convite.</summary>
    public const string HeaderChaveAcesso = "X-Chave-Acesso";

    private readonly ServicoConsulta servico;
    private readonly string chaveAcesso;
    private readonly ILogger<ConsultasController> logger;

    public ConsultasController(ServicoConsulta servico, IConfiguration configuracao, ILogger<ConsultasController> logger)
    {
        this.servico = servico;
        this.logger = logger;
        // Que a chave existe já foi conferido na subida da Api (Program.cs).
        chaveAcesso = configuracao["Consulta:ChaveAcesso"] ?? string.Empty;
    }

    [HttpPost]
    [EnableRateLimiting(PoliticasLimite.PorIp)]
    public async Task<ActionResult<RespostaConsulta>> Consultar(PedidoConsulta pedido)
    {
        if (!IsChaveValida(Request.Headers[HeaderChaveAcesso].ToString()))
        {
            return Problem("Acesso restrito: abra o app pelo link que você recebeu.", statusCode: StatusCodes.Status401Unauthorized);
        }

        string pergunta = pedido.Pergunta.Trim();
        if (pergunta.Length == 0 || pergunta.Length > PedidoConsulta.TamanhoMaximoPergunta)
        {
            return Problem($"A pergunta precisa ter entre 1 e {PedidoConsulta.TamanhoMaximoPergunta} caracteres.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!servico.ReservarConsulta())
        {
            logger.LogWarning("Limite diário de {Limite} consultas atingido.", servico.LimiteDiario);
            return Problem("O limite de perguntas de hoje foi atingido. Tente de novo amanhã.", statusCode: StatusCodes.Status429TooManyRequests);
        }

        try
        {
            return await servico.ConsultarAsync(pergunta);
        }
        catch (Exception erro)
        {
            // O detalhe (em geral do Azure OpenAI) fica no log; quem perguntou não tem o que fazer com ele.
            logger.LogError(erro, "Falha ao responder \"{Pergunta}\"", pergunta);
            return Problem("Não foi possível responder agora. Tente de novo em instantes.", statusCode: StatusCodes.Status502BadGateway);
        }
    }

    /// <summary>Comparação em tempo constante: o tempo de resposta não revela quantos caracteres batem.</summary>
    private bool IsChaveValida(string chaveRecebida)
    {
        byte[] esperada = Encoding.UTF8.GetBytes(chaveAcesso);
        byte[] recebida = Encoding.UTF8.GetBytes(chaveRecebida);
        return CryptographicOperations.FixedTimeEquals(esperada, recebida);
    }
}
