using System.Text.RegularExpressions;
using Codex20.Core.Chunking;

namespace Codex20.Chunking.Playground;

/// <summary>
/// Relatórios de diagnóstico para o chunking entity-aware — espelha o antigo
/// <c>DumpEntityBoundaries</c>: confere que nenhum chunk roubou conteúdo do vizinho e
/// que nenhuma tabela foi cortada ao meio.
/// </summary>
internal static class EntityReport
{
    private static readonly Regex CleanNameShape = new(@"^[\p{Lu}0-9][\p{Lu}0-9 ,'/()+\-\.À-ſ]{1,58}$");

    private static readonly Regex EndsClean = new(@"[\.\!\?:;""'’)\]]\s*$|\bAÇÕES\s*$", RegexOptions.IgnoreCase);

    private static readonly Regex OpenTable = new("<table", RegexOptions.IgnoreCase);

    private static readonly Regex CloseTable = new("</table>", RegexOptions.IgnoreCase);

    public static void PrintStats(List<Chunk> chunks)
    {
        var entities = chunks.Where(c => !c.IsFallback && c.EntityName is not null).ToList();
        var entitiesNoName = chunks.Where(c => !c.IsFallback && c.EntityName is null).ToList();
        int cleanNames = entities.Count(c => IsCleanName(c.EntityName!));
        int tables = chunks.Count(c => c.Text.TrimStart().StartsWith("<table", StringComparison.OrdinalIgnoreCase));
        int splitTables = chunks.Count(HasSplitTable);
        int entityChunks = entities.Count + entitiesNoName.Count;

        Console.WriteLine("──────── ESTATÍSTICAS ────────");
        Console.WriteLine($"chunks totais .............. {chunks.Count}");
        Console.WriteLine($"chunks de entidade ......... {entityChunks}");
        Console.WriteLine($"  com nome .................. {entities.Count}");
        Console.WriteLine($"  sem nome ................. {entitiesNoName.Count}");
        double pct = entityChunks == 0 ? 0 : 100.0 * cleanNames / entityChunks;
        Console.WriteLine($"  nome limpo ................ {cleanNames}/{entityChunks} ({pct:F1}%)");
        Console.WriteLine($"chunks de fallback ......... {chunks.Count(c => c.IsFallback)}");
        Console.WriteLine($"tabelas (chunk próprio) .... {tables}");
        Console.WriteLine($"tabelas cortadas ao meio ... {splitTables}   {(splitTables == 0 ? "OK" : "<<< FALHA")}");
        Console.WriteLine();

        if (entitiesNoName.Count > 0)
        {
            Console.WriteLine("Entidades SEM nome (verificar causa-raiz no Markdown bruto):");
            foreach (Chunk c in entitiesNoName)
            {
                Console.WriteLine($"  p.{c.PageStart}: {FirstLine(c.Text, 90)}");
            }

            Console.WriteLine();
        }

        var dirty = entities.Where(c => !IsCleanName(c.EntityName!)).ToList();
        if (dirty.Count > 0)
        {
            Console.WriteLine("Nomes suspeitos (verificar ExtractEntityName / FindHeaderStart):");
            foreach (Chunk c in dirty)
            {
                Console.WriteLine($"  [{c.EntityName}]  <- {FirstLine(c.Text, 70)}");
            }

            Console.WriteLine();
        }
    }

    public static void PrintSample(List<Chunk> chunks, int n)
    {
        Console.WriteLine("──────── AMOSTRA ────────");
        foreach (Chunk c in chunks.Where(c => !c.IsFallback).Take(n))
        {
            Console.WriteLine($"### {c.EntityName ?? "(sem nome)"}  [p.{c.PageStart}-{c.PageEnd}, {c.Text.Length} chars]");
            Console.WriteLine(Truncate(c.Text, 400));
            Console.WriteLine();
        }
    }

    public static void DumpEntityBoundaries(List<Chunk> chunks, bool showFallback)
    {
        Console.WriteLine("──────── FRONTEIRAS ────────");
        int flagged = 0;

        for (int i = 0; i < chunks.Count; i++)
        {
            Chunk c = chunks[i];
            if (c.IsFallback && !showFallback)
            {
                continue;
            }

            string head = FirstLine(c.Text, 95);
            string tail = LastChars(c.Text, 80);
            string tag = c.IsFallback ? "fallback" : c.EntityName ?? "(sem nome)";
            Console.WriteLine($"[{i,4}] p.{c.PageStart,-4} {tag}");
            Console.WriteLine($"       ⟨ {head}");
            Console.WriteLine($"       … {tail} ⟩");

            if (HasSplitTable(c))
            {
                Console.WriteLine("       !!! TABELA CORTADA AO MEIO");
                flagged++;
            }

            if (i + 1 < chunks.Count && !c.IsFallback && !chunks[i + 1].IsFallback)
            {
                Chunk next = chunks[i + 1];
                string trimmedEnd = c.Text.TrimEnd();
                string lastLine = trimmedEnd[(trimmedEnd.LastIndexOf('\n') + 1)..].TrimStart();
                bool endsClean = EndsClean.IsMatch(trimmedEnd)
                                 || trimmedEnd.EndsWith('m')            // "alcance 1,5 m"
                                 || trimmedEnd.EndsWith('>')            // fim de tabela
                                 || lastLine.StartsWith('·') || lastLine.StartsWith('-')  // item de lista
                                 || lastLine.StartsWith('<');
                if (!endsClean && next.EntityName is not null)
                {
                    Console.WriteLine($"       ??? termina em frase incompleta antes de '{next.EntityName}'");
                    flagged++;
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine(flagged == 0
            ? "Nenhuma fronteira suspeita."
            : $"{flagged} fronteira(s) para revisar manualmente.");
    }

    private static bool IsCleanName(string name)
    {
        string n = name.Trim();
        return n.Length >= 2 && n.Length <= 60
               && CleanNameShape.IsMatch(n.ToUpperInvariant())
               && !n.Contains("  ");
    }

    private static bool HasSplitTable(Chunk c)
    {
        return OpenTable.Matches(c.Text).Count != CloseTable.Matches(c.Text).Count;
    }

    private static string FirstLine(string text, int max)
    {
        string line = text.Replace("\n", " ⏎ ").Trim();
        return Truncate(line, max);
    }

    private static string LastChars(string text, int n)
    {
        string flat = text.Replace("\n", " ⏎ ").Trim();
        return flat.Length <= n ? flat : "…" + flat[^n..];
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
