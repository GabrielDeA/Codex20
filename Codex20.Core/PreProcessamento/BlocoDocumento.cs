namespace Codex20.Core.PreProcessamento;

/// <summary>
/// Unidade atômica produzida pelo <see cref="PreProcessadorDocumentoMarkdown"/>.
/// Um bloco é ou um parágrafo de texto (<see cref="BlocoParagrafo"/>), delimitado por
/// linha em branco real no Markdown, ou uma tabela HTML crua (<see cref="BlocoTabela"/>)
/// que nunca deve ser dividida por nenhuma estratégia de chunking.
/// </summary>
public abstract class BlocoDocumento
{
    /// <summary>
    /// Número da página (do PDF de origem) em que o bloco começa, quando conhecido.
    /// Derivado dos comentários <c>&lt;!-- PageNumber="N" --&gt;</c> do Document Intelligence.
    /// </summary>
    public int? Pagina { get; init; }

    /// <summary>Texto do bloco já normalizado (linhas unidas por <c>\n</c>).</summary>
    public abstract string Texto { get; }
}

/// <summary>Parágrafo de texto. Pode conter várias linhas (sem linha em branco entre elas).</summary>
public class BlocoParagrafo : BlocoDocumento
{
    public List<string> Linhas { get; init; } = new();

    public override string Texto => string.Join('\n', Linhas);
}

/// <summary>
/// Tabela HTML crua (<c>&lt;table&gt;...&lt;/table&gt;</c>), exatamente como o Document
/// Intelligence emitiu. Tratada como indivisível.
/// </summary>
public class BlocoTabela : BlocoDocumento
{
    public string Html { get; init; } = string.Empty;

    public override string Texto => Html;
}
