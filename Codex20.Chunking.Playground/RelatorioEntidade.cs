using System.Text.RegularExpressions;
using Codex20.Core.Chunking;

namespace Codex20.Chunking.Playground;

/// <summary>
/// Relatórios de diagnóstico para o chunking entity-aware — espelha o antigo
/// <c>DumpEntityBoundaries</c>: confere que nenhum chunk roubou conteúdo do vizinho e
/// que nenhuma tabela foi cortada ao meio.
/// </summary>
internal static class RelatorioEntidade
{
    private static readonly Regex FormatoNomeLimpo = new(@"^[\p{Lu}0-9][\p{Lu}0-9 ,'/()+\-\.À-ſ]{1,58}$");

    private static readonly Regex TerminaLimpo = new(@"[\.\!\?:;""'’)\]]\s*$|\bAÇÕES\s*$", RegexOptions.IgnoreCase);

    private static readonly Regex AbreTabela = new("<table", RegexOptions.IgnoreCase);

    private static readonly Regex FechaTabela = new("</table>", RegexOptions.IgnoreCase);

    public static void ImprimirEstatisticas(List<Chunk> chunks)
    {
        var entidades = chunks.Where(c => !c.IsFallback && c.NomeEntidade is not null).ToList();
        var entidadesSemNome = chunks.Where(c => !c.IsFallback && c.NomeEntidade is null).ToList();
        int nomesLimpos = entidades.Count(c => IsNomeLimpo(c.NomeEntidade!));
        int tabelas = chunks.Count(c => c.Texto.TrimStart().StartsWith("<table", StringComparison.OrdinalIgnoreCase));
        int tabelasCortadas = chunks.Count(IsTabelaCortada);
        int chunksEntidade = entidades.Count + entidadesSemNome.Count;

        Console.WriteLine("──────── ESTATÍSTICAS ────────");
        Console.WriteLine($"chunks totais .............. {chunks.Count}");
        Console.WriteLine($"chunks de entidade ......... {chunksEntidade}");
        Console.WriteLine($"  com nome .................. {entidades.Count}");
        Console.WriteLine($"  sem nome ................. {entidadesSemNome.Count}");
        double pct = chunksEntidade == 0 ? 0 : 100.0 * nomesLimpos / chunksEntidade;
        Console.WriteLine($"  nome limpo ................ {nomesLimpos}/{chunksEntidade} ({pct:F1}%)");
        Console.WriteLine($"chunks de fallback ......... {chunks.Count(c => c.IsFallback)}");
        Console.WriteLine($"tabelas (chunk próprio) .... {tabelas}");
        Console.WriteLine($"tabelas cortadas ao meio ... {tabelasCortadas}   {(tabelasCortadas == 0 ? "OK" : "<<< FALHA")}");
        Console.WriteLine();

        if (entidadesSemNome.Count > 0)
        {
            Console.WriteLine("Entidades SEM nome (verificar causa-raiz no Markdown bruto):");
            foreach (Chunk c in entidadesSemNome)
            {
                Console.WriteLine($"  p.{c.PaginaInicio}: {PrimeiraLinha(c.Texto, 90)}");
            }

            Console.WriteLine();
        }

        var suspeitos = entidades.Where(c => !IsNomeLimpo(c.NomeEntidade!)).ToList();
        if (suspeitos.Count > 0)
        {
            Console.WriteLine("Nomes suspeitos (verificar ExtrairNomeEntidade / AcharInicioCabecalho):");
            foreach (Chunk c in suspeitos)
            {
                Console.WriteLine($"  [{c.NomeEntidade}]  <- {PrimeiraLinha(c.Texto, 70)}");
            }

            Console.WriteLine();
        }
    }

    public static void ImprimirAmostra(List<Chunk> chunks, int n)
    {
        Console.WriteLine("──────── AMOSTRA ────────");
        foreach (Chunk c in chunks.Where(c => !c.IsFallback).Take(n))
        {
            Console.WriteLine($"### {c.NomeEntidade ?? "(sem nome)"}  [p.{c.PaginaInicio}-{c.PaginaFim}, {c.Texto.Length} chars]");
            Console.WriteLine(Truncar(c.Texto, 400));
            Console.WriteLine();
        }
    }

    public static void DespejarFronteirasEntidade(List<Chunk> chunks, bool mostrarFallback)
    {
        Console.WriteLine("──────── FRONTEIRAS ────────");
        int sinalizados = 0;

        for (int i = 0; i < chunks.Count; i++)
        {
            Chunk c = chunks[i];
            if (c.IsFallback && !mostrarFallback)
            {
                continue;
            }

            string cabeca = PrimeiraLinha(c.Texto, 95);
            string cauda = UltimosCaracteres(c.Texto, 80);
            string tag = c.IsFallback ? "fallback" : c.NomeEntidade ?? "(sem nome)";
            Console.WriteLine($"[{i,4}] p.{c.PaginaInicio,-4} {tag}");
            Console.WriteLine($"       ⟨ {cabeca}");
            Console.WriteLine($"       … {cauda} ⟩");

            if (IsTabelaCortada(c))
            {
                Console.WriteLine("       !!! TABELA CORTADA AO MEIO");
                sinalizados++;
            }

            if (i + 1 < chunks.Count && !c.IsFallback && !chunks[i + 1].IsFallback)
            {
                Chunk proximo = chunks[i + 1];
                string fimAparado = c.Texto.TrimEnd();
                string ultimaLinha = fimAparado[(fimAparado.LastIndexOf('\n') + 1)..].TrimStart();
                bool terminaLimpo = TerminaLimpo.IsMatch(fimAparado)
                                 || fimAparado.EndsWith('m')            // "alcance 1,5 m"
                                 || fimAparado.EndsWith('>')            // fim de tabela
                                 || ultimaLinha.StartsWith('·') || ultimaLinha.StartsWith('-')  // item de lista
                                 || ultimaLinha.StartsWith('<');
                if (!terminaLimpo && proximo.NomeEntidade is not null)
                {
                    Console.WriteLine($"       ??? termina em frase incompleta antes de '{proximo.NomeEntidade}'");
                    sinalizados++;
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine(sinalizados == 0
            ? "Nenhuma fronteira suspeita."
            : $"{sinalizados} fronteira(s) para revisar manualmente.");
    }

    private static bool IsNomeLimpo(string nome)
    {
        string n = nome.Trim();
        return n.Length >= 2 && n.Length <= 60
               && FormatoNomeLimpo.IsMatch(n.ToUpperInvariant())
               && !n.Contains("  ");
    }

    private static bool IsTabelaCortada(Chunk c)
    {
        return AbreTabela.Matches(c.Texto).Count != FechaTabela.Matches(c.Texto).Count;
    }

    private static string PrimeiraLinha(string texto, int max)
    {
        string linha = texto.Replace("\n", " ⏎ ").Trim();
        return Truncar(linha, max);
    }

    private static string UltimosCaracteres(string texto, int n)
    {
        string achatado = texto.Replace("\n", " ⏎ ").Trim();
        return achatado.Length <= n ? achatado : "…" + achatado[^n..];
    }

    private static string Truncar(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
