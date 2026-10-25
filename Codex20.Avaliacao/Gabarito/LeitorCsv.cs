using System.Text;

namespace Codex20.Avaliacao.Gabarito;

/// <summary>
/// CSV no formato do Excel (RFC 4180): campo entre aspas pode ter separador, quebra de linha e aspas
/// dobradas (""). A pergunta e a resposta de referência têm vírgula quase sempre, então dividir a
/// linha no separador não basta.
/// </summary>
public static class LeitorCsv
{
    /// <summary>
    /// Ponto e vírgula, que é o que o Excel em português grava (a vírgula é o separador decimal).
    /// </summary>
    public const char Separador = ';';

    /// <summary>Cada linha com o número dela no arquivo (a primeira é 1), para as mensagens de erro.</summary>
    public static List<LinhaCsv> Ler(string conteudo)
    {
        var linhas = new List<LinhaCsv>();
        var campos = new List<string>();
        var campo = new StringBuilder();
        bool isEntreAspas = false;
        int numeroLinha = 1;
        int inicioLinha = 1;

        for (int i = 0; i < conteudo.Length; i++)
        {
            char c = conteudo[i];

            if (isEntreAspas)
            {
                if (c == '"')
                {
                    if (i + 1 < conteudo.Length && conteudo[i + 1] == '"')
                    {
                        campo.Append('"');
                        i++;
                    }
                    else
                    {
                        isEntreAspas = false;
                    }
                }
                else
                {
                    if (c == '\n')
                    {
                        numeroLinha++;
                    }

                    campo.Append(c);
                }

                continue;
            }

            if (c == '"')
            {
                isEntreAspas = true;
            }
            else if (c == Separador)
            {
                campos.Add(campo.ToString());
                campo.Clear();
            }
            else if (c == '\n')
            {
                campos.Add(campo.ToString().TrimEnd('\r'));
                campo.Clear();
                linhas.Add(new LinhaCsv { Numero = inicioLinha, Campos = campos });
                campos = new List<string>();
                numeroLinha++;
                inicioLinha = numeroLinha;
            }
            else
            {
                campo.Append(c);
            }
        }

        if (campo.Length > 0 || campos.Count > 0)
        {
            campos.Add(campo.ToString().TrimEnd('\r'));
            linhas.Add(new LinhaCsv { Numero = inicioLinha, Campos = campos });
        }

        return linhas;
    }
}
