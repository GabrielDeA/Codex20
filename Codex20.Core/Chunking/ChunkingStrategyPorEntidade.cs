using Codex20.Core.Chunking.RegrasEntidade;
using Codex20.Core.PreProcessamento;

namespace Codex20.Core.Chunking;

/// <summary>
/// Strategy de chunking que ancora em um <b>bloco de atributos repetitivo</b> de cada
/// entidade do jogo, sobe parágrafo por parágrafo (delimitado por linha em branco real,
/// nunca por heading) até o cabeçalho da entidade e emite um chunk por entidade, com o
/// nome extraído. Nunca divide uma <see cref="BlocoTabela"/>. Quando nenhuma entidade é
/// reconhecida numa região, ou quando uma entidade estoura o orçamento de tokens, delega
/// à <see cref="ChunkingStrategyParagrafoToken"/> (fallback).
///
/// <para>
/// A extensão de uma entidade vai do seu cabeçalho até (o que vier primeiro) o cabeçalho
/// da próxima entidade ou o próximo heading Markdown depois da âncora — isso impede que a
/// última criatura de um grupo "engula" a introdução do grupo seguinte (ex.: <c>## DIABOS</c>
/// no Manual dos Monstros). O texto entre entidades vai para o fallback.
/// </para>
///
/// <para>
/// O comportamento por livro fica em uma implementação de <see cref="IRegrasEntidade"/>
/// (<see cref="RegrasEntidadeMonstro"/>, <see cref="RegrasEntidadeMagia"/>,
/// <see cref="RegrasEntidadeItemMagico"/>), montada pelas fábricas estáticas
/// <see cref="ParaManualDosMonstros"/> / <see cref="ParaLivroDoJogador"/> /
/// <see cref="ParaGuiaDoMestre"/>.
/// </para>
/// </summary>
public class ChunkingStrategyPorEntidade : IChunkingStrategy
{
    private readonly IRegrasEntidade _regras;
    private readonly ChunkingStrategyParagrafoToken _fallback;

    public ChunkingStrategyPorEntidade(IRegrasEntidade regras, ChunkingStrategyParagrafoToken fallback)
    {
        _regras = regras;
        _fallback = fallback;
    }

    public string Nome => _regras.Nome;

    // ---- Fábricas por livro -------------------------------------------------

    public static ChunkingStrategyPorEntidade ParaManualDosMonstros(ChunkingStrategyParagrafoToken fallback)
        => new(new RegrasEntidadeMonstro(), fallback);

    public static ChunkingStrategyPorEntidade ParaLivroDoJogador(ChunkingStrategyParagrafoToken fallback)
        => new(new RegrasEntidadeMagia(), fallback);

    public static ChunkingStrategyPorEntidade ParaGuiaDoMestre(ChunkingStrategyParagrafoToken fallback)
        => new(new RegrasEntidadeItemMagico(), fallback);

    // ---- Núcleo -----------------------------------------------------------

    /// <summary>Uma entidade localizada: onde começa o cabeçalho, onde está a âncora, e o nome.</summary>
    private class Entidade
    {
        public int Inicio { get; init; }
        public int Ancora { get; init; }
        public string? Nome { get; init; }
    }

    public List<Chunk> Chunk(List<BlocoDocumento> blocos, string livro)
    {
        (int inicioSecao, int fimSecao) = _regras.ResolverSecao(blocos);
        inicioSecao = Math.Clamp(inicioSecao, 0, blocos.Count);
        fimSecao = Math.Clamp(fimSecao, inicioSecao, blocos.Count);

        // 1. Coleta âncoras e resolve o início do cabeçalho de cada entidade (monotônico).
        var entidades = new List<Entidade>();
        int inicioAnterior = inicioSecao - 1;

        for (int i = inicioSecao; i < fimSecao; i++)
        {
            if (!_regras.IsAncora(blocos, i))
            {
                continue;
            }

            int inicioCabecalho = Math.Clamp(_regras.AcharInicioCabecalho(blocos, i), inicioAnterior + 1, i);
            entidades.Add(new Entidade
            {
                Inicio = inicioCabecalho,
                Ancora = i,
                Nome = _regras.ExtrairNomeEntidade(blocos, inicioCabecalho, i),
            });
            inicioAnterior = inicioCabecalho;
        }

        var resultado = new List<Chunk>();

        // 2. Fallback para o que vem antes da primeira entidade.
        int primeiroInicio = entidades.Count > 0 ? entidades[0].Inicio : fimSecao;
        if (primeiroInicio > 0)
        {
            resultado.AddRange(_fallback.ChunkFaixa(blocos, 0, primeiroInicio, livro, isFallback: true));
        }

        // 3. Um chunk por entidade + fallback para o "vão" até a próxima.
        for (int k = 0; k < entidades.Count; k++)
        {
            Entidade e = entidades[k];
            int proximoInicio = k + 1 < entidades.Count ? entidades[k + 1].Inicio : fimSecao;
            int fim = LimitarNoHeading(blocos, e.Ancora + 1, proximoInicio);

            EmitirEntidade(resultado, blocos, e, fim, livro);

            if (fim < proximoInicio)
            {
                resultado.AddRange(_fallback.ChunkFaixa(blocos, fim, proximoInicio, livro, isFallback: true));
            }
        }

        // 4. Fallback para o que vem depois da última entidade / fora da seção.
        int inicioCauda = entidades.Count > 0 ? fimSecao : primeiroInicio;
        if (inicioCauda < blocos.Count)
        {
            resultado.AddRange(_fallback.ChunkFaixa(blocos, inicioCauda, blocos.Count, livro, isFallback: true));
        }

        return resultado;
    }

    private void EmitirEntidade(
        List<Chunk> resultado, List<BlocoDocumento> blocos, Entidade e, int fim, string livro)
    {
        string texto = JuntarBlocos(blocos, e.Inicio, fim);
        (int? paginaInicio, int? paginaFim) = FaixaDePaginas(blocos, e.Inicio, fim);

        if (EstimarTokens(texto) <= _regras.MaxTokensPorChunk)
        {
            resultado.Add(new Chunk
            {
                Texto = texto,
                NomeEntidade = e.Nome,
                PaginaInicio = paginaInicio,
                PaginaFim = paginaFim,
                Livro = livro,
                NomeStrategy = Nome,
                IsFallback = false,
            });
            return;
        }

        // Entidade grande demais: divide, mas mantém o nome em cada pedaço.
        foreach (Chunk pedaco in _fallback.ChunkFaixa(blocos, e.Inicio, fim, livro, isFallback: false))
        {
            resultado.Add(new Chunk
            {
                Texto = pedaco.Texto,
                NomeEntidade = e.Nome,
                PaginaInicio = pedaco.PaginaInicio,
                PaginaFim = pedaco.PaginaFim,
                Livro = livro,
                NomeStrategy = Nome,
                IsFallback = false,
            });
        }
    }

    /// <summary>
    /// Primeiro bloco em <c>[de, limite)</c> que marca fronteira de entidade — um heading
    /// de outra criatura/grupo (via <see cref="IRegrasEntidade.IsHeadingFronteira"/>) ou um bloco
    /// de uma linha só que é um rótulo solto em CAIXA ALTA (legenda de figura duplicando o
    /// nome do vizinho). Se não houver, devolve <paramref name="limite"/>.
    /// </summary>
    private int LimitarNoHeading(List<BlocoDocumento> blocos, int de, int limite)
    {
        for (int j = Math.Max(0, de); j < limite; j++)
        {
            if (blocos[j] is not BlocoParagrafo p || p.Linhas.Count == 0)
            {
                continue;
            }

            if (_regras.IsHeadingFronteira(p.Linhas[0]))
            {
                return j;
            }

            if (p.Linhas.Count == 1 && IsLinhaRotuloSolto(p.Linhas[0]))
            {
                return j;
            }
        }

        return limite;
    }

    private static bool IsLinhaRotuloSolto(string linha)
    {
        string s = linha.Trim();
        if (s.Length < 4 || s.Length > 48 || s.Any(char.IsDigit) || !s.Contains(' '))
        {
            return false; // exige 2+ palavras — evita "AÇÕES", "REAÇÕES", "SUMÁRIO"
        }

        int maiusculas = s.Count(char.IsUpper);
        int minusculas = s.Count(char.IsLower);
        return maiusculas >= 3 && minusculas <= 1; // "BOTAS ÉLFICAS", "ANEL DE TELECINÉSIA"
    }

    /// <summary>Estimativa barata de tokens (~4 caracteres por token).</summary>
    private static int EstimarTokens(string texto) => texto.Length / 4;

    private static string JuntarBlocos(List<BlocoDocumento> blocos, int inicio, int fim)
    {
        var partes = new List<string>();
        for (int i = inicio; i < fim; i++)
        {
            partes.Add(blocos[i].Texto);
        }

        return string.Join("\n\n", partes);
    }

    private static (int?, int?) FaixaDePaginas(List<BlocoDocumento> blocos, int inicio, int fim)
    {
        int? primeira = null;
        int? ultima = null;
        for (int i = inicio; i < fim; i++)
        {
            if (blocos[i].Pagina is not int p)
            {
                continue;
            }

            primeira ??= p;
            ultima = p;
        }

        return (primeira, ultima);
    }
}
