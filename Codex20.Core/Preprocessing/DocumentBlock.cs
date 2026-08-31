namespace Codex20.Core.Preprocessing;

/// <summary>
/// Unidade atômica produzida pelo <see cref="MarkdownDocumentPreprocessor"/>.
/// Um bloco é ou um parágrafo de texto (<see cref="ParagraphBlock"/>), delimitado por
/// linha em branco real no Markdown, ou uma tabela HTML crua (<see cref="TableBlock"/>)
/// que nunca deve ser dividida por nenhuma estratégia de chunking.
/// </summary>
public abstract class DocumentBlock
{
    /// <summary>
    /// Número da página (do PDF de origem) em que o bloco começa, quando conhecido.
    /// Derivado dos comentários <c>&lt;!-- PageNumber="N" --&gt;</c> do Document Intelligence.
    /// </summary>
    public int? Page { get; init; }

    /// <summary>Texto do bloco já normalizado (linhas unidas por <c>\n</c>).</summary>
    public abstract string Text { get; }
}

/// <summary>Parágrafo de texto. Pode conter várias linhas (sem linha em branco entre elas).</summary>
public class ParagraphBlock : DocumentBlock
{
    public List<string> Lines { get; init; } = new();

    public override string Text => string.Join('\n', Lines);
}

/// <summary>
/// Tabela HTML crua (<c>&lt;table&gt;...&lt;/table&gt;</c>), exatamente como o Document
/// Intelligence emitiu. Tratada como indivisível.
/// </summary>
public class TableBlock : DocumentBlock
{
    public string Html { get; init; } = string.Empty;

    public override string Text => Html;
}
