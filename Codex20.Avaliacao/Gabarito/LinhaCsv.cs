namespace Codex20.Avaliacao.Gabarito;

public class LinhaCsv
{
    /// <summary>Linha do arquivo onde o registro começa (um campo entre aspas pode ocupar várias).</summary>
    public int Numero { get; init; }

    public List<string> Campos { get; init; } = new();

    public bool IsVazia
    {
        get
        {
            foreach (string campo in Campos)
            {
                if (!string.IsNullOrWhiteSpace(campo))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
