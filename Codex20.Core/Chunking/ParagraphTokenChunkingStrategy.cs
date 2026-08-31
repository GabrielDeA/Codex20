using Codex20.Core.Preprocessing;
using Microsoft.SemanticKernel.Text;

namespace Codex20.Core.Chunking;

/// <summary>
/// Baseline por parágrafo + orçamento de tokens, sobre
/// <see cref="TextChunker"/> (<c>Microsoft.SemanticKernel.Text</c>).
/// Agrupa parágrafos consecutivos até o limite de tokens, com sobreposição.
/// Cada <see cref="TableBlock"/> vira um chunk isolado — nunca entra no splitter.
/// Serve também de fallback para a <see cref="EntityAwareChunkingStrategy"/>.
/// </summary>
public class ParagraphTokenChunkingStrategy : IChunkingStrategy
{
    private readonly int _maxTokensPerChunk;
    private readonly int _overlapTokens;

    public ParagraphTokenChunkingStrategy(int maxTokensPerChunk = 512, int overlapTokens = 64)
    {
        _maxTokensPerChunk = maxTokensPerChunk;
        _overlapTokens = overlapTokens;
    }

    public string Name => "paragraph-token";

    public List<Chunk> Chunk(List<DocumentBlock> blocks, string book)
        => ChunkRange(blocks, 0, blocks.Count, book, isFallback: false);

    /// <summary>Chunka apenas <c>blocks[start..end)</c>. Usado pelo fallback entity-aware.</summary>
    public List<Chunk> ChunkRange(
        List<DocumentBlock> blocks, int start, int end, string book, bool isFallback)
    {
        var result = new List<Chunk>();
        var run = new List<ParagraphBlock>();

        void FlushRun()
        {
            if (run.Count == 0)
            {
                return;
            }

            var lines = new List<string>();
            foreach (ParagraphBlock p in run)
            {
                lines.AddRange(TextChunker.SplitPlainTextLines(p.Text, _maxTokensPerChunk));
            }

            List<string> paragraphs = TextChunker.SplitPlainTextParagraphs(
                lines, _maxTokensPerChunk, _overlapTokens);

            int? pageStart = run[0].Page;
            int? pageEnd = null;
            foreach (ParagraphBlock p in run)
            {
                if (p.Page is not null)
                {
                    pageEnd = p.Page;
                }
            }

            foreach (string paragraph in paragraphs)
            {
                result.Add(new Chunk
                {
                    Text = paragraph,
                    PageStart = pageStart,
                    PageEnd = pageEnd,
                    Book = book,
                    StrategyName = Name,
                    IsFallback = isFallback,
                });
            }

            run.Clear();
        }

        for (int i = start; i < end; i++)
        {
            switch (blocks[i])
            {
                case ParagraphBlock p:
                    run.Add(p);
                    break;
                case TableBlock t:
                    FlushRun();
                    result.Add(new Chunk
                    {
                        Text = t.Html,
                        PageStart = t.Page,
                        PageEnd = t.Page,
                        Book = book,
                        StrategyName = Name,
                        IsFallback = isFallback,
                    });
                    break;
            }
        }

        FlushRun();
        return result;
    }
}
