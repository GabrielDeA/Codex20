using Codex20.Core.Embeddings;

namespace Codex20.Core.Geracao;

/// <summary>
/// A resposta do RAG e tudo o que é preciso para auditá-la: os trechos que o modelo viu, quais ele
/// citou, o prompt exato e quanto custou em tempo e tokens.
/// </summary>
public class RespostaGerada
{
    public string Pergunta { get; init; } = string.Empty;

    /// <summary>Vazio quando o modelo não foi chamado (a busca não trouxe nada).</summary>
    public string Texto { get; init; } = string.Empty;

    public string Modelo { get; init; } = string.Empty;

    public string NomeEstrategiaBusca { get; init; } = string.Empty;

    /// <summary>Na numeração do prompt: <c>Trechos[0]</c> é o [1] da resposta.</summary>
    public List<ResultadoBusca> Trechos { get; init; } = new();

    /// <summary>Números citados que existem no prompt, na ordem da primeira citação.</summary>
    public List<int> Citacoes { get; init; } = new();

    /// <summary>
    /// Números que o modelo citou mas não existem no prompt (ex.: [9] com 8 trechos). Deveria ficar
    /// sempre vazia; quando não fica, é citação inventada.
    /// </summary>
    public List<int> CitacoesInexistentes { get; init; } = new();

    /// <summary>A mensagem exata enviada (as instruções são o <see cref="MontadorPrompt.Instrucoes"/>).</summary>
    public string Mensagem { get; init; } = string.Empty;

    public int TokensEntrada { get; init; }

    public int TokensSaida { get; init; }

    public int TokensRaciocinio { get; init; }

    public TimeSpan DuracaoBusca { get; init; }

    public TimeSpan DuracaoGeracao { get; init; }

    public bool IsCortada { get; init; }

    /// <summary>Recado para a tela: modelo não chamado, resposta cortada, citação inexistente.</summary>
    public string? Observacao { get; init; }

    public bool IsCitado(int numero)
    {
        return Citacoes.Contains(numero);
    }
}
