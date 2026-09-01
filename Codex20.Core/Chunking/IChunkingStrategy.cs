using Codex20.Core.PreProcessamento;

namespace Codex20.Core.Chunking;

/// <summary>Strategy que transforma blocos pré-processados em <see cref="Chunk"/>s.</summary>
public interface IChunkingStrategy
{
    string Nome { get; }

    List<Chunk> Chunk(List<BlocoDocumento> blocos, string livro);
}
