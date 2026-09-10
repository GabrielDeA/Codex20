namespace Codex20.Painel.Servicos;

/// <summary>O que já está gravado em <c>&lt;livro&gt;.chunks.json</c>.</summary>
public class InfoArquivoChunks
{
    public string Caminho { get; init; } = string.Empty;

    public bool IsExistente { get; init; }

    public int Quantidade { get; init; }

    public DateTime ModificadoEm { get; init; }
}
