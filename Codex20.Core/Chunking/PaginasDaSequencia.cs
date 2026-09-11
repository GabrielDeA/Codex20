using Codex20.Core.PreProcessamento;
using Microsoft.SemanticKernel.Text;

namespace Codex20.Core.Chunking;

/// <summary>
/// Descobre a faixa de páginas de cada pedaço que o <see cref="TextChunker"/> devolve para uma
/// sequência de parágrafos. O TextChunker junta as linhas com outra quebra, re-espaça o último
/// pedaço e repete palavras na sobreposição, então o texto do pedaço não aparece literalmente na
/// sequência — mas as palavras aparecem na mesma ordem. Cada palavra da sequência guarda a página
/// do parágrafo de onde veio, e o pedaço é localizado pela sua sequência de palavras.
/// Os pedaços têm de ser consultados na ordem em que o TextChunker os devolveu.
/// </summary>
internal class PaginasDaSequencia
{
    private static readonly char[] Espacos = { ' ', '\t', '\r', '\n' };

    private readonly List<string> palavras = new();
    private readonly List<int?> paginas = new();
    private int inicioPedacoAnterior;

    public PaginasDaSequencia(List<BlocoParagrafo> sequencia)
    {
        foreach (BlocoParagrafo paragrafo in sequencia)
        {
            foreach (string palavra in paragrafo.Texto.Split(Espacos, StringSplitOptions.RemoveEmptyEntries))
            {
                palavras.Add(palavra);
                paginas.Add(paragrafo.Pagina);
            }
        }
    }

    /// <summary>
    /// Primeira e última página das palavras do <paramref name="pedaco"/>. Se o pedaço não for
    /// localizado, devolve a faixa da sequência inteira — não aconteceu em nenhum dos três livros
    /// (conferido em 2026-09-10), mas o chunking não deve quebrar por causa de página.
    /// </summary>
    public (int? Inicio, int? Fim) Localizar(string pedaco)
    {
        string[] palavrasPedaco = pedaco.Split(Espacos, StringSplitOptions.RemoveEmptyEntries);
        int inicio = AcharPosicao(palavrasPedaco);
        if (inicio < 0)
        {
            return FaixaDePaginas(0, palavras.Count);
        }

        inicioPedacoAnterior = inicio;
        return FaixaDePaginas(inicio, inicio + palavrasPedaco.Length);
    }

    // A sobreposição faz o pedaço começar dentro do anterior, então a busca parte do início dele.
    private int AcharPosicao(string[] palavrasPedaco)
    {
        if (palavrasPedaco.Length == 0)
        {
            return -1;
        }

        for (int i = inicioPedacoAnterior; i + palavrasPedaco.Length <= palavras.Count; i++)
        {
            if (IsPedacoEm(i, palavrasPedaco))
            {
                return i;
            }
        }

        return -1;
    }

    private bool IsPedacoEm(int posicao, string[] palavrasPedaco)
    {
        for (int k = 0; k < palavrasPedaco.Length; k++)
        {
            if (palavras[posicao + k] != palavrasPedaco[k])
            {
                return false;
            }
        }

        return true;
    }

    private (int?, int?) FaixaDePaginas(int inicio, int fim)
    {
        int? primeira = null;
        int? ultima = null;
        for (int i = inicio; i < fim; i++)
        {
            if (paginas[i] is not int p)
            {
                continue;
            }

            primeira ??= p;
            ultima = p;
        }

        return (primeira, ultima);
    }
}
