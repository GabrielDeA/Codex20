using System.Diagnostics;
using Codex20.Core.Chunking;
using Codex20.Core.PreProcessamento;

namespace Codex20.Painel.Servicos;

/// <summary>
/// O mesmo caminho do Chunking.Playground: Markdown revisado → pré-processamento → strategy
/// por entidade, com o <c>--slice</c> e o <c>--save</c> como opções da tela.
/// </summary>
public class ServicoChunking
{
    /// <summary>Última rodada desta aba — sobrevive à navegação entre as páginas do Painel.</summary>
    public ResultadoChunking? UltimoResultado { get; private set; }

    /// <param name="recorteInicio">Primeira linha do Markdown (1-based); 0 = do começo.</param>
    /// <param name="recorteFim">Última linha, inclusiva; 0 = até o fim.</param>
    public ResultadoChunking Executar(string livro, int recorteInicio, int recorteFim)
    {
        var relogio = Stopwatch.StartNew();

        string caminho = CaminhosDados.Markdown(livro);
        if (!File.Exists(caminho))
        {
            throw new FileNotFoundException($"Markdown não encontrado: {caminho}");
        }

        string markdown = File.ReadAllText(caminho);
        int inicioEfetivo = 0;
        int fimEfetivo = 0;

        if (recorteInicio > 0 || recorteFim > 0)
        {
            string[] todas = markdown.Replace("\r\n", "\n").Split('\n');
            int a = Math.Max(1, recorteInicio) - 1;
            int b = recorteFim > 0 ? Math.Min(todas.Length, recorteFim) : todas.Length;
            if (a >= b)
            {
                throw new ArgumentException($"Recorte vazio: linhas {a + 1} a {b} (o arquivo tem {todas.Length} linhas).");
            }

            markdown = string.Join('\n', todas[a..b]);
            inicioEfetivo = a + 1;
            fimEfetivo = b;
        }

        List<BlocoDocumento> blocos = new PreProcessadorDocumentoMarkdown().Processar(markdown);
        ChunkingStrategyPorEntidade strategy = CatalogoLivros.CriarStrategy(livro);
        List<Chunk> chunks = strategy.Chunk(blocos, livro);
        relogio.Stop();

        UltimoResultado = new ResultadoChunking(livro, chunks)
        {
            NomeStrategy = strategy.Nome,
            Blocos = blocos.Count,
            BlocosParagrafo = blocos.Count(b => b is BlocoParagrafo),
            BlocosTabela = blocos.Count(b => b is BlocoTabela),
            RecorteInicio = inicioEfetivo,
            RecorteFim = fimEfetivo,
            Duracao = relogio.Elapsed,
        };

        return UltimoResultado;
    }

    /// <summary>Equivalente ao <c>--save</c>: substitui o <c>&lt;livro&gt;.chunks.json</c>.</summary>
    public string Salvar(ResultadoChunking resultado)
    {
        if (resultado.IsRecortado)
        {
            throw new InvalidOperationException("Rodada com recorte não é salva: o arquivo do livro ficaria só com o trecho.");
        }

        string destino = CaminhosDados.Chunks(resultado.Livro);
        ArquivoChunks.Salvar(destino, resultado.Chunks);
        return destino;
    }

    public InfoArquivoChunks LerInfoArquivo(string livro)
    {
        string caminho = CaminhosDados.Chunks(livro);
        if (!File.Exists(caminho))
        {
            return new InfoArquivoChunks { Caminho = caminho, IsExistente = false };
        }

        return new InfoArquivoChunks
        {
            Caminho = caminho,
            IsExistente = true,
            Quantidade = ArquivoChunks.Ler(caminho).Count,
            ModificadoEm = File.GetLastWriteTime(caminho),
        };
    }
}
