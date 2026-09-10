using Codex20.Core.Chunking;

namespace Codex20.Painel.Servicos;

/// <summary>
/// Os três livros do corpus. O nome curto é o que entra em <see cref="Chunk.Livro"/> — e,
/// portanto, no Id do chunk —, então é sempre o mesmo que o Chunking.Playground grava.
/// </summary>
public static class CatalogoLivros
{
    public static readonly List<string> Todos = new() { "monstro", "jogador", "mestre" };

    public static string NomeExibicao(string livro)
    {
        return livro switch
        {
            "monstro" => "Manual dos Monstros",
            "jogador" => "Livro do Jogador",
            "mestre" => "Guia do Mestre",
            _ => livro,
        };
    }

    public static string ArquivoMarkdown(string livro)
    {
        return livro switch
        {
            "monstro" => "RESULTADO_manualMonstro.md",
            "jogador" => "RESULTADO_LivroDoJogador.md",
            "mestre" => "RESULTADO_GuiaDoMestre.md",
            _ => throw new ArgumentException($"Livro desconhecido: {livro}"),
        };
    }

    public static ChunkingStrategyPorEntidade CriarStrategy(string livro)
    {
        var fallback = new ChunkingStrategyParagrafoToken();
        return livro switch
        {
            "monstro" => ChunkingStrategyPorEntidade.ParaManualDosMonstros(fallback),
            "jogador" => ChunkingStrategyPorEntidade.ParaLivroDoJogador(fallback),
            "mestre" => ChunkingStrategyPorEntidade.ParaGuiaDoMestre(fallback),
            _ => throw new ArgumentException($"Livro desconhecido: {livro}"),
        };
    }
}
