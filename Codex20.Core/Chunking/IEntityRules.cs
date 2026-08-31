using Codex20.Core.Preprocessing;

namespace Codex20.Core.Chunking;

/// <summary>
/// Regras de reconhecimento de entidade de um livro específico, consumidas pela
/// <see cref="EntityAwareChunkingStrategy"/>. Cada livro (Manual dos Monstros, Livro do
/// Jogador, Guia do Mestre) tem uma implementação.
/// </summary>
public interface IEntityRules
{
    /// <summary>Nome da estratégia resultante (diagnóstico), ex.: <c>"entity-aware/monstro"</c>.</summary>
    string Name { get; }

    /// <summary>
    /// Orçamento de tokens por chunk. Uma entidade acima disso é dividida pelo fallback,
    /// mantendo o nome em cada pedaço.
    /// </summary>
    int MaxTokensPerChunk { get; }

    /// <summary>
    /// Faixa <c>[Start, End)</c> de blocos onde faz sentido procurar entidades
    /// (ex.: a seção "DESCRIÇÕES DAS MAGIAS"). O que ficar fora vai para o fallback.
    /// Para o Manual dos Monstros é o documento inteiro.
    /// </summary>
    (int Start, int End) ResolveSection(List<DocumentBlock> blocks);

    /// <summary><c>true</c> se <c>blocks[index]</c> contém a âncora de atributos de uma entidade.</summary>
    bool IsAnchor(List<DocumentBlock> blocks, int index);

    /// <summary>
    /// Dada a âncora em <paramref name="anchorIndex"/>, sobe parágrafo por parágrafo e devolve
    /// o índice do bloco onde o cabeçalho da entidade começa (&lt;= <paramref name="anchorIndex"/>).
    /// </summary>
    int FindHeaderStart(List<DocumentBlock> blocks, int anchorIndex);

    /// <summary>Extrai o nome limpo da entidade a partir do(s) bloco(s) de cabeçalho; <c>null</c> se não achar.</summary>
    string? ExtractEntityName(List<DocumentBlock> blocks, int headerStart, int anchorIndex);

    /// <summary>
    /// <c>true</c> se a linha é um heading que marca início de outra criatura/grupo — usado
    /// para não deixar uma entidade "engolir" a introdução do grupo seguinte. Sub-cabeçalhos
    /// da própria ficha (<c># AÇÕES</c>) devem devolver <c>false</c>.
    /// </summary>
    bool IsBoundaryHeading(string line);
}
