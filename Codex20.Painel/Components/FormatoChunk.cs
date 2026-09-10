using Codex20.Core.Chunking;

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
}
