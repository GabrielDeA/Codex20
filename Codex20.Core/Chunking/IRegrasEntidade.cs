using Codex20.Core.PreProcessamento;

namespace Codex20.Core.Chunking;

/// <summary>
/// Regras de reconhecimento de entidade de um livro específico, consumidas pela
/// <see cref="ChunkingStrategyPorEntidade"/>. Cada livro (Manual dos Monstros, Livro do
/// Jogador, Guia do Mestre) tem uma implementação.
/// </summary>
public interface IRegrasEntidade
{
    /// <summary>Nome da estratégia resultante (diagnóstico), ex.: <c>"entity-aware/monstro"</c>.</summary>
    string Nome { get; }

    /// <summary>
    /// Orçamento de tokens por chunk. Uma entidade acima disso é dividida pelo fallback,
    /// mantendo o nome em cada pedaço.
    /// </summary>
    int MaxTokensPorChunk { get; }

    /// <summary>
    /// Faixa <c>[Inicio, Fim)</c> de blocos onde faz sentido procurar entidades
    /// (ex.: a seção "DESCRIÇÕES DAS MAGIAS"). O que ficar fora vai para o fallback.
    /// Para o Manual dos Monstros é o documento inteiro.
    /// </summary>
    (int Inicio, int Fim) ResolverSecao(List<BlocoDocumento> blocos);

    /// <summary><c>true</c> se <c>blocos[indice]</c> contém a âncora de atributos de uma entidade.</summary>
    bool IsAncora(List<BlocoDocumento> blocos, int indice);

    /// <summary>
    /// Dada a âncora em <paramref name="indiceAncora"/>, sobe parágrafo por parágrafo e devolve
    /// o índice do bloco onde o cabeçalho da entidade começa (&lt;= <paramref name="indiceAncora"/>).
    /// </summary>
    int AcharInicioCabecalho(List<BlocoDocumento> blocos, int indiceAncora);

    /// <summary>Extrai o nome limpo da entidade a partir do(s) bloco(s) de cabeçalho; <c>null</c> se não achar.</summary>
    string? ExtrairNomeEntidade(List<BlocoDocumento> blocos, int inicioCabecalho, int indiceAncora);

    /// <summary>
    /// <c>true</c> se a linha é um heading que marca início de outra criatura/grupo — usado
    /// para não deixar uma entidade "engolir" a introdução do grupo seguinte. Sub-cabeçalhos
    /// da própria ficha (<c># AÇÕES</c>) devem devolver <c>false</c>.
    /// </summary>
    bool IsHeadingFronteira(string linha);
}
