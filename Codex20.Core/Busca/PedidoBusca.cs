namespace Codex20.Core.Busca;

/// <summary>
/// O que se quer buscar. Um objeto, e não parâmetros soltos, para que filtros futuros (livro,
/// excluir fallback) entrem sem quebrar as estratégias.
/// </summary>
public class PedidoBusca
{
    public string Texto { get; init; } = string.Empty;

    public int Quantidade { get; init; } = 5;
}
