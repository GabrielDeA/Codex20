using Codex20.Core.Preprocessing;

namespace Codex20.Core.Chunking;

/// <summary>Estratégia que transforma blocos pré-processados em <see cref="Chunk"/>s.</summary>
public interface IChunkingStrategy
{
    string Name { get; }

    List<Chunk> Chunk(List<DocumentBlock> blocks, string book);
}
