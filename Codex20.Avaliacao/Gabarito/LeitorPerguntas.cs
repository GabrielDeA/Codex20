using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Codex20.Avaliacao.Gabarito;

/// <summary>
/// Lê o CSV de perguntas (<c>id;pergunta;tipo;livro;resposta_referencia;paginas_referencia;chunks_referencia;respondivel</c>).
/// Só confere o formato de cada campo; se os chunks existem é com o <see cref="ValidadorGabarito"/>.
/// </summary>
public static class LeitorPerguntas
{
    public static readonly string[] Colunas =
    {
        "id", "pergunta", "tipo", "livro", "resposta_referencia", "paginas_referencia", "chunks_referencia", "respondivel",
    };

    private static readonly string[] Tipos = { "nominal", "parafrase", "combinacao" };

    private static readonly string[] Livros = { "jogador", "mestre", "monstro" };

    /// <summary>"jogador:95" ou "jogador:95-96" (também com travessão).</summary>
    private static readonly Regex RegexPagina = new(@"^([a-z]+):(\d+)(?:[-–](\d+))?$");

    /// <summary>
    /// Listas dentro de uma célula: espaço é o documentado, mas vírgula, barra vertical e ponto e
    /// vírgula (este só com a célula entre aspas) também valem.
    /// </summary>
    private static readonly char[] SeparadoresLista = { ' ', ';', '|', ',', '\n', '\r', '\t' };

    public static List<PerguntaAvaliacao> Ler(string caminho, ProblemasGabarito problemas)
    {
        string conteudo = LerTexto(caminho, problemas);
        List<LinhaCsv> linhas = LeitorCsv.Ler(conteudo);
        var perguntas = new List<PerguntaAvaliacao>();

        if (linhas.Count == 0)
        {
            problemas.AdicionarErro(1, "arquivo vazio.");
            return perguntas;
        }

        // Lido com o separador errado, o cabeçalho inteiro vira uma coluna só.
        if (linhas[0].Campos.Count == 1 && linhas[0].Campos[0].Contains(','))
        {
            problemas.AdicionarErro(linhas[0].Numero, $"o separador do CSV é ponto e vírgula ({LeitorCsv.Separador}), e o cabeçalho usa vírgula.");
            return perguntas;
        }

        Dictionary<string, int> indicePorColuna = LerCabecalho(linhas[0], problemas);
        if (!problemas.IsValido)
        {
            return perguntas;
        }

        for (int i = 1; i < linhas.Count; i++)
        {
            LinhaCsv linha = linhas[i];
            if (linha.IsVazia)
            {
                continue;
            }

            PerguntaAvaliacao? pergunta = LerPergunta(linha, indicePorColuna, problemas);
            if (pergunta is not null)
            {
                perguntas.Add(pergunta);
            }
        }

        return perguntas;
    }

    /// <summary>
    /// O "CSV UTF-8" do Excel vem com BOM e cai no primeiro caso. O "CSV" simples vem em Windows-1252,
    /// que não é UTF-8 válido quando há acento: lido como UTF-8, viraria "?" sem aviso nenhum.
    /// </summary>
    private static string LerTexto(string caminho, ProblemasGabarito problemas)
    {
        // Compartilhado: com o CSV aberto no Excel, o File.ReadAllBytes falha com "being used by
        // another process". Lê o que está salvo no disco, não o que está na tela do Excel.
        byte[] bytes;
        using (var arquivo = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var memoria = new MemoryStream())
        {
            arquivo.CopyTo(memoria);
            bytes = memoria.ToArray();
        }

        var utf8Estrito = new UTF8Encoding(false, true);

        try
        {
            string texto = utf8Estrito.GetString(bytes);
            return texto.TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            problemas.AdicionarAviso(1, "o arquivo não está em UTF-8; lido como Latin-1. No Excel, salve como \"CSV UTF-8\".");
            return Encoding.Latin1.GetString(bytes);
        }
    }

    private static Dictionary<string, int> LerCabecalho(LinhaCsv cabecalho, ProblemasGabarito problemas)
    {
        var indicePorColuna = new Dictionary<string, int>();
        for (int i = 0; i < cabecalho.Campos.Count; i++)
        {
            string nome = Normalizar(cabecalho.Campos[i]);
            if (nome.Length > 0)
            {
                indicePorColuna.TryAdd(nome, i);
            }
        }

        foreach (string coluna in Colunas)
        {
            if (!indicePorColuna.ContainsKey(coluna))
            {
                problemas.AdicionarErro(cabecalho.Numero, $"falta a coluna '{coluna}' no cabeçalho (esperado: {string.Join(LeitorCsv.Separador, Colunas)}).");
            }
        }

        return indicePorColuna;
    }

    private static PerguntaAvaliacao? LerPergunta(LinhaCsv linha, Dictionary<string, int> indicePorColuna, ProblemasGabarito problemas)
    {
        int errosAntes = problemas.Erros.Count;

        string id = Campo(linha, indicePorColuna, "id");
        string texto = Campo(linha, indicePorColuna, "pergunta");
        string tipo = Normalizar(Campo(linha, indicePorColuna, "tipo"));
        string livro = Campo(linha, indicePorColuna, "livro").ToLowerInvariant();
        string respondivel = Normalizar(Campo(linha, indicePorColuna, "respondivel"));

        if (id.Length == 0)
        {
            problemas.AdicionarErro(linha.Numero, "id vazio.");
        }

        if (texto.Length == 0)
        {
            problemas.AdicionarErro(linha.Numero, "pergunta vazia.");
        }

        if (!Tipos.Contains(tipo))
        {
            problemas.AdicionarAviso(linha.Numero, $"tipo '{tipo}' não é nominal, parafrase nem combinacao: não entra no recorte por tipo.");
        }

        bool isRespondivel = false;
        if (respondivel == "sim" || respondivel == "s" || respondivel == "true" || respondivel == "1")
        {
            isRespondivel = true;
        }
        else if (respondivel != "nao" && respondivel != "n" && respondivel != "false" && respondivel != "0")
        {
            problemas.AdicionarErro(linha.Numero, $"respondivel '{respondivel}' deveria ser sim ou nao.");
        }

        var paginas = new List<IntervaloPaginas>();
        foreach (string parte in Campo(linha, indicePorColuna, "paginas_referencia").ToLowerInvariant().Split(SeparadoresLista, StringSplitOptions.RemoveEmptyEntries))
        {
            Match pagina = RegexPagina.Match(parte);
            string livroPagina = pagina.Success ? Apelido(pagina.Groups[1].Value) : string.Empty;
            if (!pagina.Success || !Livros.Contains(livroPagina))
            {
                problemas.AdicionarErro(linha.Numero, $"página '{parte}' fora do formato livro:pagina (ex. jogador:95 ou mestre:12-13).");
                continue;
            }

            int inicio = int.Parse(pagina.Groups[2].Value);
            int fim = pagina.Groups[3].Success ? int.Parse(pagina.Groups[3].Value) : inicio;
            paginas.Add(new IntervaloPaginas { Livro = livroPagina, Inicio = inicio, Fim = Math.Max(inicio, fim) });
        }

        var chunks = new List<Guid>();
        foreach (string parte in Campo(linha, indicePorColuna, "chunks_referencia").Split(SeparadoresLista, StringSplitOptions.RemoveEmptyEntries))
        {
            if (Guid.TryParse(parte, out Guid chunkId))
            {
                if (!chunks.Contains(chunkId))
                {
                    chunks.Add(chunkId);
                }
            }
            else
            {
                problemas.AdicionarErro(linha.Numero, $"'{parte}' não é um Id de chunk (copie o \"Id\" do *.chunks.json ou da saída de --pagina/--localizar).");
            }
        }

        if (problemas.Erros.Count > errosAntes)
        {
            return null;
        }

        return new PerguntaAvaliacao
        {
            Id = id,
            Pergunta = texto,
            Tipo = tipo,
            Livro = livro,
            RespostaReferencia = Campo(linha, indicePorColuna, "resposta_referencia"),
            PaginasReferencia = paginas,
            ChunksReferencia = chunks,
            IsRespondivel = isRespondivel,
            Linha = linha.Numero,
        };
    }

    private static string Campo(LinhaCsv linha, Dictionary<string, int> indicePorColuna, string coluna)
    {
        int indice = indicePorColuna[coluna];
        return indice < linha.Campos.Count ? linha.Campos[indice].Trim() : string.Empty;
    }

    /// <summary>
    /// O apelido gravado em <c>Chunk.Livro</c> a partir do jeito que se escreve no CSV: "monstros" e
    /// "manual" (do Manual dos Monstros) viram "monstro", como no Embeddings.Playground.
    /// </summary>
    private static string Apelido(string livro)
    {
        if (livro == "monstros" || livro == "manual")
        {
            return "monstro";
        }

        return livro;
    }

    /// <summary>Minúsculas e sem acento: "Paráfrase" e "parafrase", "Não" e "nao" valem o mesmo.</summary>
    public static string Normalizar(string texto)
    {
        string decomposto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder();
        foreach (char c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                resultado.Append(c);
            }
        }

        return resultado.ToString().Normalize(NormalizationForm.FormC);
    }
}
