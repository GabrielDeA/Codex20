using Codex20.Avaliacao.Gabarito;
using Codex20.Avaliacao.Registros;

namespace Codex20.Avaliacao.Metricas;

/// <summary>
/// As contas do Passo 4, sobre os registros do JSONL. Só aritmética: quem escolhe o recorte
/// (estratégia, tipo, modo) é o <see cref="RelatorioMetricas"/>.
/// </summary>
public static class CalculadoraMetricas
{
    /// <summary>Posição do 1º chunk de referência na estratégia; <c>null</c> = fora da profundidade.</summary>
    public static int? Posicao(RegistroPergunta registro, string estrategia)
    {
        foreach (RegistroRecuperacao recuperacao in registro.Recuperacao)
        {
            if (recuperacao.Estrategia == estrategia)
            {
                return recuperacao.PosicaoPrimeiraReferencia;
            }
        }

        throw new InvalidOperationException($"A pergunta {registro.Id} não tem a estratégia {estrategia} no JSONL.");
    }

    public static bool IsHit(int? posicao, int k)
    {
        return posicao is not null && posicao <= k;
    }

    public static double HitAt(List<int?> posicoes, int k)
    {
        if (posicoes.Count == 0)
        {
            return 0;
        }

        int acertos = 0;
        foreach (int? posicao in posicoes)
        {
            if (IsHit(posicao, k))
            {
                acertos++;
            }
        }

        return (double)acertos / posicoes.Count;
    }

    /// <summary>Média de 1/posição, contando 0 quando a referência ficou abaixo de k.</summary>
    public static double Mrr(List<int?> posicoes, int k)
    {
        if (posicoes.Count == 0)
        {
            return 0;
        }

        double soma = 0;
        foreach (int? posicao in posicoes)
        {
            if (IsHit(posicao, k))
            {
                soma += 1.0 / posicao!.Value;
            }
        }

        return soma / posicoes.Count;
    }

    /// <summary>
    /// Teste de McNemar exato (binomial, bicaudal) sobre os pares discordantes: as mesmas perguntas
    /// nas duas estratégias, então o que importa é onde uma acerta e a outra erra.
    /// </summary>
    public static double McNemar(int soA, int soB)
    {
        int n = soA + soB;
        if (n == 0)
        {
            return 1;
        }

        int menor = Math.Min(soA, soB);
        double cauda = 0;
        for (int i = 0; i <= menor; i++)
        {
            cauda += Combinacao(n, i) * Math.Pow(0.5, n);
        }

        return Math.Min(1, 2 * cauda);
    }

    private static double Combinacao(int n, int k)
    {
        double resultado = 1;
        for (int i = 1; i <= k; i++)
        {
            resultado = resultado * (n - k + i) / i;
        }

        return resultado;
    }

    /// <summary>
    /// Por chunk: os <see cref="RegistroGeracao.ChunksCitados"/> contra os chunks de referência. Só faz
    /// sentido com RAG.
    /// </summary>
    public static Prf PrfChunks(RegistroGeracao geracao, List<Guid> referencias)
    {
        var citados = new List<Guid>();
        foreach (Guid chunkId in geracao.ChunksCitados)
        {
            if (!citados.Contains(chunkId))
            {
                citados.Add(chunkId);
            }
        }

        int certos = 0;
        foreach (Guid chunkId in citados)
        {
            if (referencias.Contains(chunkId))
            {
                certos++;
            }
        }

        int cobertas = 0;
        foreach (Guid chunkId in referencias)
        {
            if (citados.Contains(chunkId))
            {
                cobertas++;
            }
        }

        return Prf.Calcular(certos, citados.Count, cobertas, referencias.Count);
    }

    /// <summary>
    /// Por página: uma página citada está certa se cai numa página de referência do mesmo livro. É o
    /// nível que compara com × sem RAG, porque sem RAG não há chunk.
    /// </summary>
    public static Prf PrfPaginas(RegistroGeracao geracao, List<IntervaloPaginas> referencias)
    {
        int certos = 0;
        foreach (IntervaloPaginas citada in geracao.PaginasCitadas)
        {
            if (IsSobrepoeAlguma(citada, referencias))
            {
                certos++;
            }
        }

        int cobertas = 0;
        foreach (IntervaloPaginas referencia in referencias)
        {
            if (IsSobrepoeAlguma(referencia, geracao.PaginasCitadas))
            {
                cobertas++;
            }
        }

        return Prf.Calcular(certos, geracao.PaginasCitadas.Count, cobertas, referencias.Count);
    }

    /// <summary>
    /// Por livro: o nível mais grosso, o único em que o modo sem RAG costuma citar alguma coisa. Os
    /// livros de referência saem das páginas de referência.
    /// </summary>
    public static Prf PrfLivros(RegistroGeracao geracao, List<IntervaloPaginas> paginasReferencia)
    {
        List<string> referencias = LivrosDe(paginasReferencia);

        int certos = 0;
        foreach (string livro in geracao.LivrosCitados)
        {
            if (referencias.Contains(livro))
            {
                certos++;
            }
        }

        int cobertas = 0;
        foreach (string livro in referencias)
        {
            if (geracao.LivrosCitados.Contains(livro))
            {
                cobertas++;
            }
        }

        return Prf.Calcular(certos, geracao.LivrosCitados.Count, cobertas, referencias.Count);
    }

    public static List<string> LivrosDe(List<IntervaloPaginas> paginas)
    {
        var livros = new List<string>();
        foreach (IntervaloPaginas pagina in paginas)
        {
            if (!livros.Contains(pagina.Livro))
            {
                livros.Add(pagina.Livro);
            }
        }

        return livros;
    }

    private static bool IsSobrepoeAlguma(IntervaloPaginas intervalo, List<IntervaloPaginas> outros)
    {
        foreach (IntervaloPaginas outro in outros)
        {
            if (outro.IsSobrepoe(intervalo.Livro, intervalo.Inicio, intervalo.Fim))
            {
                return true;
            }
        }

        return false;
    }

    public static double Media(List<double> valores)
    {
        if (valores.Count == 0)
        {
            return 0;
        }

        double soma = 0;
        foreach (double valor in valores)
        {
            soma += valor;
        }

        return soma / valores.Count;
    }

    /// <summary>Percentil pelo método do posto mais próximo; p em 0–100.</summary>
    public static double Percentil(List<double> valores, double p)
    {
        if (valores.Count == 0)
        {
            return 0;
        }

        var ordenados = new List<double>(valores);
        ordenados.Sort();
        int posto = (int)Math.Ceiling(p / 100 * ordenados.Count);
        return ordenados[Math.Clamp(posto - 1, 0, ordenados.Count - 1)];
    }
}
