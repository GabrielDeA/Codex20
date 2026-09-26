namespace Codex20.Core.Geracao;

/// <summary>
/// Um modelo de linguagem que escreve a resposta. Não sabe nada de RAG: recebe instruções e a
/// mensagem já montadas pelo <see cref="MontadorPrompt"/>, então trocar de modelo não mexe no prompt.
/// </summary>
public interface IGeradorResposta
{
    /// <summary>Nome do deployment — vai para a tela e para as tabelas do trabalho.</summary>
    string Modelo { get; }

    Task<ConclusaoModelo> GerarAsync(string instrucoes, string mensagem);
}
