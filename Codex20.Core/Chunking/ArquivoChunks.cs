using System.Text.Json;
using System.Text.Encodings.Web;

namespace Codex20.Core.Chunking;

/// <summary>
/// Checkpoint do chunking em JSON: separa a fase de chunking da fase de embedding, para não
/// refazer uma quando só a outra mudou.
/// </summary>
public static class ArquivoChunks
{
    private static readonly JsonSerializerOptions Opcoes = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static void Salvar(string caminho, List<Chunk> chunks)
    {
        string? diretorio = Path.GetDirectoryName(caminho);
        if (!string.IsNullOrEmpty(diretorio))
        {
            Directory.CreateDirectory(diretorio);
        }

        File.WriteAllText(caminho, JsonSerializer.Serialize(chunks, Opcoes));
    }

    public static List<Chunk> Ler(string caminho)
    {
        string json = File.ReadAllText(caminho);
        return JsonSerializer.Deserialize<List<Chunk>>(json, Opcoes)
            ?? throw new InvalidOperationException($"JSON de chunks vazio ou inválido: {caminho}");
    }
}
