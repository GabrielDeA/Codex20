namespace Codex20.Avaliacao.Gabarito;

/// <summary>
/// O que a leitura e a conferência do gabarito acharam. Erro impede a avaliação de rodar; aviso só é
/// mostrado.
/// </summary>
public class ProblemasGabarito
{
    public List<string> Erros { get; } = new();

    public List<string> Avisos { get; } = new();

    public bool IsValido => Erros.Count == 0;

    public void AdicionarErro(int linha, string mensagem)
    {
        Erros.Add($"linha {linha}: {mensagem}");
    }

    public void AdicionarAviso(int linha, string mensagem)
    {
        Avisos.Add($"linha {linha}: {mensagem}");
    }
}
