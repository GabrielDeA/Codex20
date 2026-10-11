namespace Codex20.App.Servicos;

/// <summary>Falha de uma consulta, já com a mensagem pronta para mostrar a quem perguntou.</summary>
public class ErroConsulta : Exception
{
    public ErroConsulta(string mensagem, bool isAcessoNegado) : base(mensagem)
    {
        IsAcessoNegado = isAcessoNegado;
    }

    /// <summary>A Api recusou a chave: a tela volta a pedir o código de acesso.</summary>
    public bool IsAcessoNegado { get; }
}
