using Codex20.Core.PreProcessamento;
using Microsoft.SemanticKernel.Text;

namespace Codex20.Core.Chunking;

/// <summary>
/// Baseline por parágrafo + orçamento de tokens, sobre
/// <see cref="TextChunker"/> (<c>Microsoft.SemanticKernel.Text</c>).
/// Agrupa parágrafos consecutivos até o limite de tokens, com sobreposição.
/// Cada <see cref="BlocoTabela"/> vira um chunk isolado — nunca entra no splitter.
/// Serve também de fallback para a <see cref="ChunkingStrategyPorEntidade"/>.
/// </summary>
public class ChunkingStrategyParagrafoToken : IChunkingStrategy
{
    private readonly int _maxTokensPorChunk;
    private readonly int _tokensSobreposicao;

    public ChunkingStrategyParagrafoToken(int maxTokensPorChunk = 512, int tokensSobreposicao = 64)
    {
        _maxTokensPorChunk = maxTokensPorChunk;
        _tokensSobreposicao = tokensSobreposicao;
    }

    public string Nome => "paragraph-token";

    public List<Chunk> Chunk(List<BlocoDocumento> blocos, string livro)
        => ChunkFaixa(blocos, 0, blocos.Count, livro, isFallback: false);

    /// <summary>Chunka apenas <c>blocos[inicio..fim)</c>. Usado pelo fallback entity-aware.</summary>
    public List<Chunk> ChunkFaixa(
        List<BlocoDocumento> blocos, int inicio, int fim, string livro, bool isFallback)
    {
        var resultado = new List<Chunk>();
        var sequencia = new List<BlocoParagrafo>();

        void DescarregarSequencia()
        {
            if (sequencia.Count == 0)
            {
                return;
            }

            var linhas = new List<string>();
            foreach (BlocoParagrafo p in sequencia)
            {
                linhas.AddRange(TextChunker.SplitPlainTextLines(p.Texto, _maxTokensPorChunk));
            }

            List<string> paragrafos = TextChunker.SplitPlainTextParagraphs(
                linhas, _maxTokensPorChunk, _tokensSobreposicao);

            int? paginaInicio = sequencia[0].Pagina;
            int? paginaFim = null;
            foreach (BlocoParagrafo p in sequencia)
            {
                if (p.Pagina is not null)
                {
                    paginaFim = p.Pagina;
                }
            }

            foreach (string paragrafo in paragrafos)
            {
                resultado.Add(new Chunk
                {
                    Texto = paragrafo,
                    PaginaInicio = paginaInicio,
                    PaginaFim = paginaFim,
                    Livro = livro,
                    NomeStrategy = Nome,
                    IsFallback = isFallback,
                });
            }

            sequencia.Clear();
        }

        for (int i = inicio; i < fim; i++)
        {
            switch (blocos[i])
            {
                case BlocoParagrafo p:
                    sequencia.Add(p);
                    break;
                case BlocoTabela t:
                    DescarregarSequencia();
                    resultado.Add(new Chunk
                    {
                        Texto = t.Html,
                        PaginaInicio = t.Pagina,
                        PaginaFim = t.Pagina,
                        Livro = livro,
                        NomeStrategy = Nome,
                        IsFallback = isFallback,
                    });
                    break;
            }
        }

        DescarregarSequencia();
        return resultado;
    }
}
