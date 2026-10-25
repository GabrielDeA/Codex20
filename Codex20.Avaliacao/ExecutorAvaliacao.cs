using System.Diagnostics;
using Codex20.Avaliacao.Gabarito;
using Codex20.Avaliacao.Registros;
using Codex20.Core.Busca;
using Codex20.Core.Embeddings;
using Codex20.Core.Geracao;

namespace Codex20.Avaliacao;

/// <summary>
/// Roda uma pergunta do gabarito por tudo o que o trabalho compara: as três buscas, a geração com RAG
/// (a mesma configuração da Api) e a geração sem RAG.
/// </summary>
public class ExecutorAvaliacao
{
    private readonly List<IEstrategiaBusca> estrategias;
    private readonly IEstrategiaBusca estrategiaGeracao;
    private readonly PipelineRag pipeline;
    private readonly int profundidade;
    private readonly int quantidadeTrechos;
    private readonly int repeticoes;

    /// <param name="estrategias">As que entram na comparação de recuperação.</param>
    /// <param name="estrategiaGeracao">A que alimenta a geração com RAG (RRF, como na Api).</param>
    /// <param name="profundidade">
    /// Até onde procurar o chunk de referência no ranking. Fundo de propósito: "fora do top 8" e "13º"
    /// contam igual no Hit@8, mas não na análise de erros.
    /// </param>
    public ExecutorAvaliacao(List<IEstrategiaBusca> estrategias, IEstrategiaBusca estrategiaGeracao, PipelineRag pipeline,
        int profundidade, int quantidadeTrechos, int repeticoes)
    {
        this.estrategias = estrategias;
        this.estrategiaGeracao = estrategiaGeracao;
        this.pipeline = pipeline;
        this.profundidade = profundidade;
        this.quantidadeTrechos = quantidadeTrechos;
        this.repeticoes = repeticoes;
    }

    public async Task<RegistroPergunta> AvaliarAsync(PerguntaAvaliacao pergunta)
    {
        // Pergunta sem resposta nos livros não tem chunk de referência: a busca roda igual (o Top fica
        // registrado), mas a posição sai nula e ela fica fora do Hit@k.
        var chunksReferencia = new List<Guid>();
        if (pergunta.IsRespondivel)
        {
            chunksReferencia.AddRange(pergunta.ChunksReferencia);
        }

        var registro = new RegistroPergunta
        {
            Id = pergunta.Id,
            Pergunta = pergunta.Pergunta,
            Tipo = pergunta.Tipo,
            Livro = pergunta.Livro,
            IsRespondivel = pergunta.IsRespondivel,
            ChunksReferencia = chunksReferencia,
            PaginasReferencia = pergunta.PaginasReferencia,
        };

        // Em série, como no Painel: em paralelo as estratégias disputariam CPU e o arquivo do banco.
        foreach (IEstrategiaBusca estrategia in estrategias)
        {
            registro.Recuperacao.Add(await BuscarAsync(estrategia, pergunta.Pergunta, chunksReferencia));
        }

        var pedido = new PedidoResposta { Pergunta = pergunta.Pergunta, QuantidadeTrechos = quantidadeTrechos };
        for (int rodada = 1; rodada <= repeticoes; rodada++)
        {
            RespostaGerada comRag = await pipeline.ResponderAsync(estrategiaGeracao, pedido);
            registro.Geracoes.Add(RegistrarGeracao(comRag, RegistroGeracao.ModoComRag, rodada, chunksReferencia));

            RespostaGerada semRag = await pipeline.ResponderSemRagAsync(pedido);
            registro.Geracoes.Add(RegistrarGeracao(semRag, RegistroGeracao.ModoSemRag, rodada, chunksReferencia));
        }

        return registro;
    }

    private async Task<RegistroRecuperacao> BuscarAsync(IEstrategiaBusca estrategia, string texto, List<Guid> chunksReferencia)
    {
        var relogio = Stopwatch.StartNew();
        RespostaBusca resposta = await estrategia.BuscarAsync(new PedidoBusca { Texto = texto, Quantidade = profundidade });
        TimeSpan duracao = relogio.Elapsed;

        var posicoes = new List<PosicaoReferencia>();
        int? primeira = null;
        foreach (Guid chunkId in chunksReferencia)
        {
            int? posicao = null;
            for (int i = 0; i < resposta.Resultados.Count; i++)
            {
                if (resposta.Resultados[i].Chunk.Id == chunkId)
                {
                    posicao = i + 1;
                    break;
                }
            }

            posicoes.Add(new PosicaoReferencia { ChunkId = chunkId, Posicao = posicao });
            if (posicao is not null && (primeira is null || posicao < primeira))
            {
                primeira = posicao;
            }
        }

        var top = new List<RegistroTrecho>();
        for (int i = 0; i < resposta.Resultados.Count && i < quantidadeTrechos; i++)
        {
            top.Add(RegistroTrecho.Criar(resposta.Resultados[i], i + 1, chunksReferencia));
        }

        return new RegistroRecuperacao
        {
            Estrategia = estrategia.Nome,
            Profundidade = profundidade,
            PosicaoPrimeiraReferencia = primeira,
            PosicoesReferencia = posicoes,
            Top = top,
            DuracaoMs = Milissegundos(duracao),
            Observacao = resposta.Observacao,
        };
    }

    private static RegistroGeracao RegistrarGeracao(RespostaGerada resposta, string modo, int rodada, List<Guid> chunksReferencia)
    {
        var trechos = new List<RegistroTrecho>();
        for (int i = 0; i < resposta.Trechos.Count; i++)
        {
            trechos.Add(RegistroTrecho.Criar(resposta.Trechos[i], i + 1, chunksReferencia));
        }

        var chunksCitados = new List<Guid>();
        var paginasCitadas = new List<IntervaloPaginas>();
        var livrosCitados = new List<string>();
        foreach (int numero in resposta.Citacoes)
        {
            ResultadoBusca trecho = resposta.Trechos[numero - 1];
            chunksCitados.Add(trecho.Chunk.Id);
            if (!livrosCitados.Contains(trecho.Chunk.Livro))
            {
                livrosCitados.Add(trecho.Chunk.Livro);
            }

            if (trecho.Chunk.PaginaInicio is not null)
            {
                int inicio = trecho.Chunk.PaginaInicio.Value;
                int fim = trecho.Chunk.PaginaFim ?? inicio;
                paginasCitadas.Add(new IntervaloPaginas { Livro = trecho.Chunk.Livro, Inicio = inicio, Fim = fim });
            }
        }

        if (resposta.IsSemRag)
        {
            paginasCitadas = LeitorFontesCitadas.Extrair(resposta.Texto);
            livrosCitados = LeitorFontesCitadas.ExtrairLivros(resposta.Texto);
        }

        return new RegistroGeracao
        {
            Modo = modo,
            Rodada = rodada,
            Modelo = resposta.Modelo,
            Texto = resposta.Texto,
            Trechos = trechos,
            Citacoes = resposta.Citacoes,
            ChunksCitados = chunksCitados,
            CitacoesInexistentes = resposta.CitacoesInexistentes,
            PaginasCitadas = paginasCitadas,
            LivrosCitados = livrosCitados,
            TokensEntrada = resposta.TokensEntrada,
            TokensSaida = resposta.TokensSaida,
            TokensRaciocinio = resposta.TokensRaciocinio,
            DuracaoBuscaMs = Milissegundos(resposta.DuracaoBusca),
            DuracaoGeracaoMs = Milissegundos(resposta.DuracaoGeracao),
            IsCortada = resposta.IsCortada,
            Observacao = resposta.Observacao,
        };
    }

    private static double Milissegundos(TimeSpan duracao)
    {
        return Math.Round(duracao.TotalMilliseconds, 1);
    }
}
