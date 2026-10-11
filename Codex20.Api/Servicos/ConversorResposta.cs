using Codex20.Contratos;
using Codex20.Core.Chunking;
using Codex20.Core.Geracao;

namespace Codex20.Api.Servicos;

/// <summary>
/// Transforma a <see cref="RespostaGerada"/> do pipeline no que o App recebe. É aqui que o texto
/// dos trechos fica para trás: das fontes só saem livro, páginas e nome da entidade.
/// </summary>
public static class ConversorResposta
{
    public static RespostaConsulta Converter(RespostaGerada resposta)
    {
        // O prompt numera os 8 trechos, mas o modelo cita só alguns ([2] e [5], por exemplo). Para
        // quem lê, as fontes são renumeradas 1, 2, 3… na ordem da primeira citação.
        var numeroNovo = new Dictionary<int, int>();
        var fontes = new List<FonteConsulta>();
        foreach (int numeroPrompt in resposta.Citacoes)
        {
            Chunk chunk = resposta.Trechos[numeroPrompt - 1].Chunk;
            var fonte = new FonteConsulta
            {
                Numero = fontes.Count + 1,
                Livro = NomesLivros.Exibicao(chunk.Livro),
                PaginaInicio = chunk.PaginaInicio,
                PaginaFim = chunk.PaginaFim,
                NomeEntidade = chunk.NomeEntidade,
            };

            numeroNovo[numeroPrompt] = fonte.Numero;
            fontes.Add(fonte);
        }

        return new RespostaConsulta
        {
            Partes = MontarPartes(resposta.Texto, numeroNovo),
            Fontes = fontes,
            Aviso = MontarAviso(resposta),
        };
    }

    private static List<ParteResposta> MontarPartes(string texto, Dictionary<int, int> numeroNovo)
    {
        var partes = new List<ParteResposta>();
        foreach (SegmentoResposta segmento in LeitorCitacoes.Segmentar(texto))
        {
            if (!segmento.IsCitacao)
            {
                partes.Add(new ParteResposta { Texto = segmento.Texto });
                continue;
            }

            // Citação a trecho que não estava no prompt ("[9]" com 8 trechos) simplesmente some:
            // não há fonte para mostrar. O Painel é que serve para investigar esses casos.
            var jaCitadas = new List<int>();
            foreach (int numeroPrompt in segmento.Numeros)
            {
                if (numeroNovo.ContainsKey(numeroPrompt) && !jaCitadas.Contains(numeroNovo[numeroPrompt]))
                {
                    jaCitadas.Add(numeroNovo[numeroPrompt]);
                    partes.Add(new ParteResposta { NumeroFonte = numeroNovo[numeroPrompt] });
                }
            }
        }

        return partes;
    }

    /// <summary>
    /// A <see cref="RespostaGerada.Observacao"/> é técnica (fala de prompt e de tokens) e fica no log.
    /// Para quem perguntou vai só o que muda a leitura da resposta.
    /// </summary>
    private static string? MontarAviso(RespostaGerada resposta)
    {
        if (resposta.Trechos.Count == 0)
        {
            return "Não encontrei nada nos livros sobre isso. Tente perguntar de outro jeito.";
        }

        if (resposta.Texto.Length == 0)
        {
            return "Não consegui escrever uma resposta desta vez. Tente de novo.";
        }

        if (resposta.IsCortada)
        {
            return "A resposta ficou longa demais e pode estar incompleta.";
        }

        return null;
    }
}
