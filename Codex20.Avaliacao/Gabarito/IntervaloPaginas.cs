namespace Codex20.Avaliacao.Gabarito;

/// <summary>
/// Páginas de um livro: as de referência no gabarito ("jogador:95-96") ou as que uma resposta citou.
/// </summary>
public class IntervaloPaginas
{
    /// <summary>Apelido, como em <c>Chunk.Livro</c>: jogador, mestre ou monstro.</summary>
    public string Livro { get; init; } = string.Empty;

    public int Inicio { get; init; }

    /// <summary>Igual a <see cref="Inicio"/> quando é uma página só.</summary>
    public int Fim { get; init; }

    public bool IsSobrepoe(string livro, int inicio, int fim)
    {
        return Livro == livro && Inicio <= fim && inicio <= Fim;
    }

    public override string ToString()
    {
        return Inicio == Fim ? $"{Livro}:{Inicio}" : $"{Livro}:{Inicio}-{Fim}";
    }
}
