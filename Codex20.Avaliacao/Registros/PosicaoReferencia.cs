namespace Codex20.Avaliacao.Registros;

/// <summary>Onde um chunk de referência caiu no ranking de uma estratégia.</summary>
public class PosicaoReferencia
{
    public Guid ChunkId { get; init; }

    /// <summary>A partir de 1; <c>null</c> quando ficou fora da profundidade buscada.</summary>
    public int? Posicao { get; init; }
}
