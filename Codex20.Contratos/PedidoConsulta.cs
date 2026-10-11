namespace Codex20.Contratos;

/// <summary>Uma pergunta enviada pelo App.</summary>
public class PedidoConsulta
{
    public const int TamanhoMaximoPergunta = 500;

    public string Pergunta { get; set; } = string.Empty;
}
