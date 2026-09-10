using Codex20.Core.Chunking;

namespace Codex20.Painel.Servicos;

/// <summary>
/// Uma rodada de chunking com as mesmas estatísticas que o Chunking.Playground imprime. Os
/// problemas ficam guardados como índices em <see cref="Chunks"/>, para a tela poder pular
/// direto até o chunk.
/// </summary>
public class ResultadoChunking
{
    private readonly HashSet<int> indicesComProblema = new();

    public ResultadoChunking(string livro, List<Chunk> chunks)
    {
        Livro = livro;
        Chunks = chunks;

        for (int i = 0; i < chunks.Count; i++)
        {
            Chunk chunk = chunks[i];

            if (chunk.IsFallback)
            {
                ChunksFallback++;
            }
            else
            {
                ChunksEntidade++;
                if (chunk.NomeEntidade is null)
                {
                    IndicesSemNome.Add(i);
                }
                else if (DiagnosticoChunks.IsNomeLimpo(chunk.NomeEntidade))
                {
                    NomesLimpos++;
                }
                else
                {
                    IndicesNomeSuspeito.Add(i);
                }
            }

            if (DiagnosticoChunks.IsTabela(chunk))
            {
                Tabelas++;
            }

            if (DiagnosticoChunks.IsTabelaCortada(chunk))
            {
                IndicesTabelaCortada.Add(i);
            }

            if (i + 1 < chunks.Count && DiagnosticoChunks.IsFronteiraSuspeita(chunk, chunks[i + 1]))
            {
                IndicesFronteiraSuspeita.Add(i);
            }
        }

        indicesComProblema.UnionWith(IndicesSemNome);
        indicesComProblema.UnionWith(IndicesNomeSuspeito);
        indicesComProblema.UnionWith(IndicesTabelaCortada);
        indicesComProblema.UnionWith(IndicesFronteiraSuspeita);
    }

    public string Livro { get; }

    public List<Chunk> Chunks { get; }

    public string NomeStrategy { get; init; } = string.Empty;

    public int Blocos { get; init; }

    public int BlocosParagrafo { get; init; }

    public int BlocosTabela { get; init; }

    /// <summary>Primeira linha do recorte (1-based); 0 quando rodou o livro inteiro.</summary>
    public int RecorteInicio { get; init; }

    /// <summary>Última linha do recorte (inclusiva); 0 quando rodou o livro inteiro.</summary>
    public int RecorteFim { get; init; }

    public bool IsRecortado => RecorteInicio > 0;

    public TimeSpan Duracao { get; init; }

    public int ChunksEntidade { get; }

    public int ChunksFallback { get; }

    public int NomesLimpos { get; }

    public int Tabelas { get; }

    public int ComNome => ChunksEntidade - IndicesSemNome.Count;

    public double PercentualNomeLimpo => ChunksEntidade == 0 ? 0 : 100.0 * NomesLimpos / ChunksEntidade;

    public List<int> IndicesSemNome { get; } = new();

    public List<int> IndicesNomeSuspeito { get; } = new();

    public List<int> IndicesTabelaCortada { get; } = new();

    /// <summary>Chunks de entidade que terminam em frase incompleta antes da entidade seguinte.</summary>
    public List<int> IndicesFronteiraSuspeita { get; } = new();

    public int QuantidadeComProblema => indicesComProblema.Count;

    public bool IsComProblema(int indice)
    {
        return indicesComProblema.Contains(indice);
    }

    public List<string> DescreverProblemas(int indice)
    {
        var problemas = new List<string>();

        if (IndicesSemNome.Contains(indice))
        {
            problemas.Add("Entidade sem nome — verificar a causa-raiz no Markdown bruto.");
        }

        if (IndicesNomeSuspeito.Contains(indice))
        {
            problemas.Add("Nome suspeito — verificar ExtrairNomeEntidade / AcharInicioCabecalho.");
        }

        if (IndicesTabelaCortada.Contains(indice))
        {
            problemas.Add("Tabela cortada ao meio.");
        }

        if (IndicesFronteiraSuspeita.Contains(indice))
        {
            problemas.Add($"Termina em frase incompleta antes de '{Chunks[indice + 1].NomeEntidade}'.");
        }

        return problemas;
    }
}
