using Codex20.Core.Chunking.EntityRules;
using Codex20.Core.Preprocessing;

namespace Codex20.Core.Chunking;

/// <summary>
/// Estratégia de chunking que ancora em um <b>bloco de atributos repetitivo</b> de cada
/// entidade do jogo, sobe parágrafo por parágrafo (delimitado por linha em branco real,
/// nunca por heading) até o cabeçalho da entidade e emite um chunk por entidade, com o
/// nome extraído. Nunca divide uma <see cref="TableBlock"/>. Quando nenhuma entidade é
/// reconhecida numa região, ou quando uma entidade estoura o orçamento de tokens, delega
/// ao <see cref="ParagraphTokenChunkingStrategy"/> (fallback).
///
/// <para>
/// A extensão de uma entidade vai do seu cabeçalho até (o que vier primeiro) o cabeçalho
/// da próxima entidade ou o próximo heading Markdown depois da âncora — isso impede que a
/// última criatura de um grupo "engula" a introdução do grupo seguinte (ex.: <c>## DIABOS</c>
/// no Manual dos Monstros). O texto entre entidades vai para o fallback.
/// </para>
///
/// <para>
/// O comportamento por livro fica em uma implementação de <see cref="IEntityRules"/>
/// (<see cref="MonsterEntityRules"/>, <see cref="SpellEntityRules"/>,
/// <see cref="MagicItemEntityRules"/>), montada pelas fábricas estáticas
/// <see cref="ForManualDosMonstros"/> / <see cref="ForLivroDoJogador"/> /
/// <see cref="ForGuiaDoMestre"/>.
/// </para>
/// </summary>
public class EntityAwareChunkingStrategy : IChunkingStrategy
{
    private readonly IEntityRules _rules;
    private readonly ParagraphTokenChunkingStrategy _fallback;

    public EntityAwareChunkingStrategy(IEntityRules rules, ParagraphTokenChunkingStrategy fallback)
    {
        _rules = rules;
        _fallback = fallback;
    }

    public string Name => _rules.Name;

    // ---- Fábricas por livro -------------------------------------------------

    public static EntityAwareChunkingStrategy ForManualDosMonstros(ParagraphTokenChunkingStrategy fallback)
        => new(new MonsterEntityRules(), fallback);

    public static EntityAwareChunkingStrategy ForLivroDoJogador(ParagraphTokenChunkingStrategy fallback)
        => new(new SpellEntityRules(), fallback);

    public static EntityAwareChunkingStrategy ForGuiaDoMestre(ParagraphTokenChunkingStrategy fallback)
        => new(new MagicItemEntityRules(), fallback);

    // ---- Núcleo -----------------------------------------------------------

    /// <summary>Uma entidade localizada: onde começa o cabeçalho, onde está a âncora, e o nome.</summary>
    private class Entity
    {
        public int Start { get; init; }
        public int Anchor { get; init; }
        public string? Name { get; init; }
    }

    public List<Chunk> Chunk(List<DocumentBlock> blocks, string book)
    {
        (int sectionStart, int sectionEnd) = _rules.ResolveSection(blocks);
        sectionStart = Math.Clamp(sectionStart, 0, blocks.Count);
        sectionEnd = Math.Clamp(sectionEnd, sectionStart, blocks.Count);

        // 1. Coleta âncoras e resolve o início do cabeçalho de cada entidade (monotônico).
        var entities = new List<Entity>();
        int prevStart = sectionStart - 1;

        for (int i = sectionStart; i < sectionEnd; i++)
        {
            if (!_rules.IsAnchor(blocks, i))
            {
                continue;
            }

            int headerStart = Math.Clamp(_rules.FindHeaderStart(blocks, i), prevStart + 1, i);
            entities.Add(new Entity
            {
                Start = headerStart,
                Anchor = i,
                Name = _rules.ExtractEntityName(blocks, headerStart, i),
            });
            prevStart = headerStart;
        }

        var result = new List<Chunk>();

        // 2. Fallback para o que vem antes da primeira entidade.
        int firstStart = entities.Count > 0 ? entities[0].Start : sectionEnd;
        if (firstStart > 0)
        {
            result.AddRange(_fallback.ChunkRange(blocks, 0, firstStart, book, isFallback: true));
        }

        // 3. Um chunk por entidade + fallback para o "vão" até a próxima.
        for (int k = 0; k < entities.Count; k++)
        {
            Entity e = entities[k];
            int nextStart = k + 1 < entities.Count ? entities[k + 1].Start : sectionEnd;
            int end = CapAtHeading(blocks, e.Anchor + 1, nextStart);

            EmitEntity(result, blocks, e, end, book);

            if (end < nextStart)
            {
                result.AddRange(_fallback.ChunkRange(blocks, end, nextStart, book, isFallback: true));
            }
        }

        // 4. Fallback para o que vem depois da última entidade / fora da seção.
        int tailStart = entities.Count > 0 ? sectionEnd : firstStart;
        if (tailStart < blocks.Count)
        {
            result.AddRange(_fallback.ChunkRange(blocks, tailStart, blocks.Count, book, isFallback: true));
        }

        return result;
    }

    private void EmitEntity(
        List<Chunk> result, List<DocumentBlock> blocks, Entity e, int end, string book)
    {
        string text = JoinBlocks(blocks, e.Start, end);
        (int? pageStart, int? pageEnd) = PageRange(blocks, e.Start, end);

        if (EstimateTokens(text) <= _rules.MaxTokensPerChunk)
        {
            result.Add(new Chunk
            {
                Text = text,
                EntityName = e.Name,
                PageStart = pageStart,
                PageEnd = pageEnd,
                Book = book,
                StrategyName = Name,
                IsFallback = false,
            });
            return;
        }

        // Entidade grande demais: divide, mas mantém o nome em cada pedaço.
        foreach (Chunk piece in _fallback.ChunkRange(blocks, e.Start, end, book, isFallback: false))
        {
            result.Add(new Chunk
            {
                Text = piece.Text,
                EntityName = e.Name,
                PageStart = piece.PageStart,
                PageEnd = piece.PageEnd,
                Book = book,
                StrategyName = Name,
                IsFallback = false,
            });
        }
    }

    /// <summary>
    /// Primeiro bloco em <c>[from, limit)</c> que marca fronteira de entidade — um heading
    /// de outra criatura/grupo (via <see cref="IEntityRules.IsBoundaryHeading"/>) ou um bloco
    /// de uma linha só que é um rótulo solto em CAIXA ALTA (legenda de figura duplicando o
    /// nome do vizinho). Se não houver, devolve <paramref name="limit"/>.
    /// </summary>
    private int CapAtHeading(List<DocumentBlock> blocks, int from, int limit)
    {
        for (int j = Math.Max(0, from); j < limit; j++)
        {
            if (blocks[j] is not ParagraphBlock p || p.Lines.Count == 0)
            {
                continue;
            }

            if (_rules.IsBoundaryHeading(p.Lines[0]))
            {
                return j;
            }

            if (p.Lines.Count == 1 && IsBareLabelLine(p.Lines[0]))
            {
                return j;
            }
        }

        return limit;
    }

    private static bool IsBareLabelLine(string line)
    {
        string s = line.Trim();
        if (s.Length < 4 || s.Length > 48 || s.Any(char.IsDigit) || !s.Contains(' '))
        {
            return false; // exige 2+ palavras — evita "AÇÕES", "REAÇÕES", "SUMÁRIO"
        }

        int upper = s.Count(char.IsUpper);
        int lower = s.Count(char.IsLower);
        return upper >= 3 && lower <= 1; // "BOTAS ÉLFICAS", "ANEL DE TELECINÉSIA"
    }

    /// <summary>Estimativa barata de tokens (~4 caracteres por token).</summary>
    private static int EstimateTokens(string text) => text.Length / 4;

    private static string JoinBlocks(List<DocumentBlock> blocks, int start, int end)
    {
        var parts = new List<string>();
        for (int i = start; i < end; i++)
        {
            parts.Add(blocks[i].Text);
        }

        return string.Join("\n\n", parts);
    }

    private static (int?, int?) PageRange(List<DocumentBlock> blocks, int start, int end)
    {
        int? first = null;
        int? last = null;
        for (int i = start; i < end; i++)
        {
            if (blocks[i].Page is not int p)
            {
                continue;
            }

            first ??= p;
            last = p;
        }

        return (first, last);
    }
}
