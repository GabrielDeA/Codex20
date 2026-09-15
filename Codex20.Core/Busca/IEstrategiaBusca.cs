namespace Codex20.Core.Busca;

/// <summary>
/// Uma forma de recuperar chunks para uma consulta. As implementações diferem só no critério de
/// ranqueamento — quem escolhe qual usar é quem chama.
/// </summary>
public interface IEstrategiaBusca
{
    /// <summary>Chave técnica, estável: vai no select, no CLI e nas tabelas do trabalho.</summary>
    string Nome { get; }

    string NomeExibicao { get; }

    /// <summary>Uma linha sobre o critério, mostrada abaixo do formulário de busca.</summary>
    string Descricao { get; }

    /// <summary>Como ler a pontuação desta estratégia — escalas diferentes não se comparam.</summary>
    string RotuloPontuacao { get; }

    /// <summary>
    /// <c>false</c> quando falta algo para rodar (ex.: a vetorial sem credenciais do Azure OpenAI).
    /// A tela desabilita a opção em vez de deixar estourar na hora da busca.
    /// </summary>
    bool IsDisponivel { get; }

    /// <summary>Preenchido quando <see cref="IsDisponivel"/> é <c>false</c>.</summary>
    string? MotivoIndisponivel { get; }

    Task<RespostaBusca> BuscarAsync(PedidoBusca pedido);
}
