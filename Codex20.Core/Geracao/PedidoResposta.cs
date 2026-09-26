namespace Codex20.Core.Geracao;

/// <summary>Uma pergunta para o RAG responder.</summary>
public class PedidoResposta
{
    public string Pergunta { get; init; } = string.Empty;

    /// <summary>
    /// Quantos chunks da busca vão para o prompt. Com os chunks medindo ~1.300 caracteres em média
    /// (~350 tokens), 8 trechos dão ~3 mil tokens de contexto: cabe folgado e ainda cobre a ficha
    /// de criatura, que é o chunk grande.
    /// </summary>
    public int QuantidadeTrechos { get; init; } = 8;
}
