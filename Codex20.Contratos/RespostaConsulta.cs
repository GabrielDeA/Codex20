namespace Codex20.Contratos;

/// <summary>
/// O que o App mostra: a resposta e de onde ela veio. O texto dos trechos nunca sai da Api — só o
/// livro, a página e o nome da entidade —, para não redistribuir o conteúdo dos livros.
/// </summary>
public class RespostaConsulta
{
    /// <summary>A resposta já separada em texto corrido e citações, na ordem do texto.</summary>
    public List<ParteResposta> Partes { get; set; } = new();

    /// <summary>
    /// Só as fontes citadas, numeradas de 1 em diante na ordem da primeira citação. É essa a
    /// numeração que <see cref="ParteResposta.NumeroFonte"/> usa.
    /// </summary>
    public List<FonteConsulta> Fontes { get; set; } = new();

    /// <summary>Recado para quem perguntou: nada encontrado, resposta incompleta.</summary>
    public string? Aviso { get; set; }
}
