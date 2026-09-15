using Codex20.Core.Busca;
using Codex20.Core.Chunking;
using Codex20.Core.Embeddings;

namespace Codex20.Painel.Components;

/// <summary>Textos curtos para mostrar um chunk em listas e cabeçalhos.</summary>
public static class FormatoChunk
{
    public static string Titulo(Chunk chunk)
    {
        if (chunk.NomeEntidade is not null)
        {
            return chunk.NomeEntidade;
        }

        return chunk.IsFallback ? "(fallback)" : "(sem nome)";
    }

    public static string Paginas(Chunk chunk)
    {
        if (chunk.PaginaInicio is null)
        {
            return "p. ?";
        }

        if (chunk.PaginaFim is null || chunk.PaginaFim == chunk.PaginaInicio)
        {
            return $"p. {chunk.PaginaInicio}";
        }

        return $"p. {chunk.PaginaInicio}–{chunk.PaginaFim}";
    }

    /// <summary>Começo do texto numa linha só — é o que identifica um chunk de fallback na lista.</summary>
    public static string Inicio(Chunk chunk, int maximo)
    {
        string linha = chunk.Texto.Replace('\n', ' ').Trim();
        return linha.Length <= maximo ? linha : linha[..maximo] + "…";
    }

    /// <summary>"cosseno #3 · bm25 —" — a colocação em cada perna de um resultado fundido.</summary>
    public static string Contribuicoes(ResultadoBusca resultado)
    {
        var partes = new List<string>();
        foreach (ContribuicaoEstrategia contribuicao in resultado.Contribuicoes)
        {
            string posicao = contribuicao.Posicao > 0 ? $"#{contribuicao.Posicao}" : "—";
            partes.Add($"{contribuicao.NomeEstrategia} {posicao}");
        }

        return string.Join(" · ", partes);
    }
}
