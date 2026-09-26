namespace Codex20.Core.Chunking;

/// <summary>
/// Nome por extenso de cada livro a partir do apelido gravado em <see cref="Chunk.Livro"/>. Fica no
/// Core porque o prompt da geração também cita o livro, não só a tela.
/// </summary>
public static class NomesLivros
{
    public static string Exibicao(string livro)
    {
        return livro switch
        {
            "monstro" => "Manual dos Monstros",
            "jogador" => "Livro do Jogador",
            "mestre" => "Guia do Mestre",
            _ => livro,
        };
    }
}
