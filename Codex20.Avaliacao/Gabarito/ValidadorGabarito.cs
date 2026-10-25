using Codex20.Core.Chunking;

namespace Codex20.Avaliacao.Gabarito;

/// <summary>
/// Confere o gabarito contra os <c>*.chunks.json</c> atuais. O <c>Chunk.Id</c> é hash do texto: se o
/// chunking mudou depois que o gabarito foi escrito, os Ids antigos deixam de existir e a avaliação
/// contaria erro de busca onde só há gabarito velho. Por isso Id desconhecido é erro, não aviso.
/// </summary>
public static class ValidadorGabarito
{
    public static void Validar(List<PerguntaAvaliacao> perguntas, Dictionary<Guid, Chunk> chunksPorId, ProblemasGabarito problemas)
    {
        var ids = new HashSet<string>();

        foreach (PerguntaAvaliacao pergunta in perguntas)
        {
            if (!ids.Add(pergunta.Id))
            {
                problemas.AdicionarErro(pergunta.Linha, $"id '{pergunta.Id}' repetido.");
            }

            if (pergunta.IsRespondivel && pergunta.ChunksReferencia.Count == 0)
            {
                problemas.AdicionarErro(pergunta.Linha, "pergunta respondível sem chunks_referencia: não há como medir a busca.");
            }

            if (!pergunta.IsRespondivel && pergunta.ChunksReferencia.Count > 0)
            {
                problemas.AdicionarAviso(pergunta.Linha, "pergunta marcada como sem resposta, mas com chunks_referencia; eles serão ignorados no Hit@k.");
            }

            if (pergunta.IsRespondivel && pergunta.PaginasReferencia.Count == 0)
            {
                problemas.AdicionarAviso(pergunta.Linha, "sem paginas_referencia: a P/R/F1 por página (com × sem RAG) não terá gabarito.");
            }

            foreach (Guid chunkId in pergunta.ChunksReferencia)
            {
                if (!chunksPorId.TryGetValue(chunkId, out Chunk? chunk))
                {
                    problemas.AdicionarErro(pergunta.Linha, $"chunk {chunkId} não existe nos *.chunks.json atuais (o chunking mudou depois do gabarito?).");
                    continue;
                }

                // Pega Id colado da linha errada: o chunk tem de cair numa das páginas de referência.
                if (pergunta.PaginasReferencia.Count > 0 && !IsNasPaginas(chunk, pergunta.PaginasReferencia))
                {
                    problemas.AdicionarAviso(pergunta.Linha,
                        $"chunk {chunkId} ({chunk.Livro}, p. {DescreverPaginas(chunk)}) não está em nenhuma das páginas de referência.");
                }
            }
        }
    }

    private static bool IsNasPaginas(Chunk chunk, List<IntervaloPaginas> paginas)
    {
        if (chunk.PaginaInicio is null)
        {
            return false;
        }

        int inicio = chunk.PaginaInicio.Value;
        int fim = chunk.PaginaFim ?? inicio;

        foreach (IntervaloPaginas intervalo in paginas)
        {
            if (intervalo.IsSobrepoe(chunk.Livro, inicio, fim))
            {
                return true;
            }
        }

        return false;
    }

    public static string DescreverPaginas(Chunk chunk)
    {
        if (chunk.PaginaInicio is null)
        {
            return "?";
        }

        if (chunk.PaginaFim is null || chunk.PaginaFim == chunk.PaginaInicio)
        {
            return chunk.PaginaInicio.ToString()!;
        }

        return $"{chunk.PaginaInicio}-{chunk.PaginaFim}";
    }
}
