using System.Globalization;
using System.Text;
using Codex20.Avaliacao.Registros;

namespace Codex20.Avaliacao.Metricas;

/// <summary>
/// Monta o relatório do Passo 4 em Markdown, com as tabelas no formato de colar no texto do TCC.
/// Números no padrão brasileiro (vírgula decimal).
/// </summary>
public class RelatorioMetricas
{
    private static readonly CultureInfo Cultura = new("pt-BR");

    private static readonly int[] Ks = { 1, 3, 5, 8 };

    /// <summary>O mesmo número de trechos que vai para o prompt: além dele, o modelo não vê o chunk.</summary>
    private const int KMrr = 8;

    private static readonly string[] OrdemTipos = { "nominal", "parafrase", "combinacao" };

    private readonly List<RegistroPergunta> registros;
    private readonly List<RegistroPergunta> respondiveis = new();
    private readonly List<string> estrategias = new();
    private readonly List<string> tipos = new();
    private readonly StringBuilder texto = new();

    public RelatorioMetricas(List<RegistroPergunta> registros)
    {
        this.registros = registros;

        foreach (RegistroPergunta registro in registros)
        {
            if (registro.IsRespondivel)
            {
                respondiveis.Add(registro);
            }

            foreach (RegistroRecuperacao recuperacao in registro.Recuperacao)
            {
                if (!estrategias.Contains(recuperacao.Estrategia))
                {
                    estrategias.Add(recuperacao.Estrategia);
                }
            }
        }

        foreach (string tipo in OrdemTipos)
        {
            tipos.Add(tipo);
        }

        foreach (RegistroPergunta registro in respondiveis)
        {
            if (!tipos.Contains(registro.Tipo))
            {
                tipos.Add(registro.Tipo);
            }
        }
    }

    public string Montar(string arquivo, MetadadosAvaliacao? metadados)
    {
        texto.Clear();
        EscreverCabecalho(arquivo, metadados);
        EscreverRecuperacao();
        EscreverRecuperacaoPorTipo();
        EscreverSignificancia();
        EscreverFontes();
        EscreverDesempenho();
        EscreverPorPergunta();
        EscreverNotas();
        return texto.ToString();
    }

    private void EscreverCabecalho(string arquivo, MetadadosAvaliacao? metadados)
    {
        texto.AppendLine("# Métricas da avaliação");
        texto.AppendLine();
        texto.AppendLine($"- Arquivo: `{arquivo}`");
        if (metadados is not null)
        {
            texto.AppendLine($"- Rodada de {metadados.DataHora.ToString("dd/MM/yyyy HH:mm", Cultura)}, modelo {metadados.Modelo}, embedding {metadados.ModeloEmbedding}");
            texto.AppendLine($"- Geração com RAG: {metadados.EstrategiaGeracao}, {metadados.QuantidadeTrechos} trechos; posição procurada até a {metadados.Profundidade}ª");
        }

        int rodadas = 0;
        foreach (RegistroPergunta registro in registros)
        {
            foreach (RegistroGeracao geracao in registro.Geracoes)
            {
                rodadas = Math.Max(rodadas, geracao.Rodada);
            }
        }

        texto.AppendLine($"- {registros.Count} perguntas: {respondiveis.Count} com resposta nos livros, {registros.Count - respondiveis.Count} sem; {rodadas} rodada(s) de geração");
        texto.AppendLine();
    }

    private void EscreverRecuperacao()
    {
        texto.AppendLine($"## Recuperação (n = {respondiveis.Count})");
        texto.AppendLine();
        texto.AppendLine("Posição do primeiro chunk de referência no ranking de cada estratégia, nas perguntas com resposta nos livros.");
        texto.AppendLine();
        EscreverTabelaRecuperacao(respondiveis, true);
    }

    private void EscreverTabelaRecuperacao(List<RegistroPergunta> perguntas, bool isComForaDoTop)
    {
        var cabecalho = new StringBuilder("| Estratégia |");
        var separador = new StringBuilder("| --- |");
        foreach (int k in Ks)
        {
            cabecalho.Append($" Hit@{k} |");
            separador.Append(" ---: |");
        }

        cabecalho.Append($" MRR@{KMrr} |");
        separador.Append(" ---: |");
        if (isComForaDoTop)
        {
            cabecalho.Append(" Fora do top 50 |");
            separador.Append(" ---: |");
        }

        texto.AppendLine(cabecalho.ToString());
        texto.AppendLine(separador.ToString());

        foreach (string estrategia in estrategias)
        {
            List<int?> posicoes = Posicoes(perguntas, estrategia);
            var linha = new StringBuilder($"| {estrategia} |");
            foreach (int k in Ks)
            {
                linha.Append($" {Pct(CalculadoraMetricas.HitAt(posicoes, k))} |");
            }

            linha.Append($" {Dec(CalculadoraMetricas.Mrr(posicoes, KMrr))} |");
            if (isComForaDoTop)
            {
                int fora = 0;
                foreach (int? posicao in posicoes)
                {
                    if (posicao is null)
                    {
                        fora++;
                    }
                }

                linha.Append($" {fora} |");
            }

            texto.AppendLine(linha.ToString());
        }

        texto.AppendLine();
    }

    private void EscreverRecuperacaoPorTipo()
    {
        texto.AppendLine($"### Hit@{KMrr} por tipo de pergunta");
        texto.AppendLine();

        var cabecalho = new StringBuilder("| Tipo | n |");
        var separador = new StringBuilder("| --- | ---: |");
        foreach (string estrategia in estrategias)
        {
            cabecalho.Append($" {estrategia} |");
            separador.Append(" ---: |");
        }

        texto.AppendLine(cabecalho.ToString());
        texto.AppendLine(separador.ToString());

        foreach (string tipo in tipos)
        {
            List<RegistroPergunta> doTipo = DoTipo(tipo);
            if (doTipo.Count == 0)
            {
                continue;
            }

            var linha = new StringBuilder($"| {tipo} | {doTipo.Count} |");
            foreach (string estrategia in estrategias)
            {
                linha.Append($" {Pct(CalculadoraMetricas.HitAt(Posicoes(doTipo, estrategia), KMrr))} |");
            }

            texto.AppendLine(linha.ToString());
        }

        texto.AppendLine();

        foreach (string tipo in tipos)
        {
            List<RegistroPergunta> doTipo = DoTipo(tipo);
            if (doTipo.Count == 0)
            {
                continue;
            }

            texto.AppendLine($"#### {tipo} (n = {doTipo.Count})");
            texto.AppendLine();
            EscreverTabelaRecuperacao(doTipo, false);
        }
    }

    private void EscreverSignificancia()
    {
        texto.AppendLine("### Significância (McNemar exato)");
        texto.AppendLine();
        texto.AppendLine("Pares discordantes: perguntas em que só uma das duas estratégias acertou. p < 0,05 indica diferença que dificilmente é acaso.");
        texto.AppendLine();
        texto.AppendLine("| Par (A × B) | k | Só A acerta | Só B acerta | p |");
        texto.AppendLine("| --- | ---: | ---: | ---: | ---: |");

        for (int i = 0; i < estrategias.Count; i++)
        {
            for (int j = i + 1; j < estrategias.Count; j++)
            {
                foreach (int k in new[] { 1, 5, KMrr })
                {
                    int soA = 0;
                    int soB = 0;
                    foreach (RegistroPergunta registro in respondiveis)
                    {
                        bool isA = CalculadoraMetricas.IsHit(CalculadoraMetricas.Posicao(registro, estrategias[i]), k);
                        bool isB = CalculadoraMetricas.IsHit(CalculadoraMetricas.Posicao(registro, estrategias[j]), k);
                        if (isA && !isB)
                        {
                            soA++;
                        }
                        else if (isB && !isA)
                        {
                            soB++;
                        }
                    }

                    double p = CalculadoraMetricas.McNemar(soA, soB);
                    texto.AppendLine($"| {estrategias[i]} × {estrategias[j]} | {k} | {soA} | {soB} | {p.ToString("F3", Cultura)} |");
                }
            }
        }

        texto.AppendLine();
    }

    private void EscreverFontes()
    {
        texto.AppendLine($"## Fontes citadas (n = {respondiveis.Count})");
        texto.AppendLine();
        texto.AppendLine("Média por resposta. Sem citação nenhuma, precisão, recall e F1 contam como 0.");
        texto.AppendLine();
        texto.AppendLine("| Modo | Nível | Precisão | Recall | F1 | Ao menos uma certa | Sem citação |");
        texto.AppendLine("| --- | --- | ---: | ---: | ---: | ---: | ---: |");

        EscreverLinhaFontes(RegistroGeracao.ModoComRag, "chunk");
        EscreverLinhaFontes(RegistroGeracao.ModoComRag, "página");
        EscreverLinhaFontes(RegistroGeracao.ModoSemRag, "página");
        EscreverLinhaFontes(RegistroGeracao.ModoComRag, "livro");
        EscreverLinhaFontes(RegistroGeracao.ModoSemRag, "livro");
        texto.AppendLine();
    }

    private void EscreverLinhaFontes(string modo, string nivel)
    {
        var resultados = new List<Prf>();
        foreach (RegistroPergunta registro in respondiveis)
        {
            foreach (RegistroGeracao geracao in registro.Geracoes)
            {
                if (geracao.Modo != modo)
                {
                    continue;
                }

                if (nivel == "chunk")
                {
                    resultados.Add(CalculadoraMetricas.PrfChunks(geracao, registro.ChunksReferencia));
                }
                else if (registro.PaginasReferencia.Count == 0)
                {
                    // Sem página de referência não há gabarito para esse nível.
                    continue;
                }
                else if (nivel == "página")
                {
                    resultados.Add(CalculadoraMetricas.PrfPaginas(geracao, registro.PaginasReferencia));
                }
                else
                {
                    resultados.Add(CalculadoraMetricas.PrfLivros(geracao, registro.PaginasReferencia));
                }
            }
        }

        var precisoes = new List<double>();
        var recalls = new List<double>();
        var f1s = new List<double>();
        int algumaCerta = 0;
        int semCitacao = 0;
        foreach (Prf prf in resultados)
        {
            precisoes.Add(prf.Precisao);
            recalls.Add(prf.Recall);
            f1s.Add(prf.F1);
            if (prf.IsAlgumaCerta)
            {
                algumaCerta++;
            }

            if (prf.IsSemCitacao)
            {
                semCitacao++;
            }
        }

        double fracaoCerta = resultados.Count == 0 ? 0 : (double)algumaCerta / resultados.Count;
        texto.AppendLine($"| {modo} | {nivel} | {Dec(CalculadoraMetricas.Media(precisoes))} | {Dec(CalculadoraMetricas.Media(recalls))} | "
            + $"{Dec(CalculadoraMetricas.Media(f1s))} | {Pct(fracaoCerta)} | {semCitacao} de {resultados.Count} |");
    }

    private void EscreverDesempenho()
    {
        texto.AppendLine($"## Desempenho (n = {registros.Count})");
        texto.AppendLine();
        texto.AppendLine("### Tempo de busca por estratégia (ms)");
        texto.AppendLine();
        texto.AppendLine("Inclui a chamada à API de embeddings no cosseno e no RRF. A primeira consulta paga o aquecimento; a mediana não se afeta.");
        texto.AppendLine();
        texto.AppendLine("| Estratégia | Média | Mediana | p95 |");
        texto.AppendLine("| --- | ---: | ---: | ---: |");
        foreach (string estrategia in estrategias)
        {
            var tempos = new List<double>();
            foreach (RegistroPergunta registro in registros)
            {
                foreach (RegistroRecuperacao recuperacao in registro.Recuperacao)
                {
                    if (recuperacao.Estrategia == estrategia)
                    {
                        tempos.Add(recuperacao.DuracaoMs);
                    }
                }
            }

            EscreverLinhaTempos(estrategia, tempos);
        }

        texto.AppendLine();
        texto.AppendLine("### Geração");
        texto.AppendLine();
        texto.AppendLine("| Modo | Tempo médio (s) | Mediana (s) | p95 (s) | Tokens de entrada (média) | Tokens de saída (média) | Raciocínio (média) | Total entrada | Total saída |");
        texto.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (string modo in new[] { RegistroGeracao.ModoComRag, RegistroGeracao.ModoSemRag })
        {
            var tempos = new List<double>();
            var entradas = new List<double>();
            var saidas = new List<double>();
            var raciocinios = new List<double>();
            foreach (RegistroPergunta registro in registros)
            {
                foreach (RegistroGeracao geracao in registro.Geracoes)
                {
                    if (geracao.Modo == modo)
                    {
                        // Com RAG o usuário espera a busca e a geração: as duas entram no tempo.
                        tempos.Add((geracao.DuracaoBuscaMs + geracao.DuracaoGeracaoMs) / 1000);
                        entradas.Add(geracao.TokensEntrada);
                        saidas.Add(geracao.TokensSaida);
                        raciocinios.Add(geracao.TokensRaciocinio);
                    }
                }
            }

            texto.AppendLine($"| {modo} | {Seg(CalculadoraMetricas.Media(tempos))} | {Seg(CalculadoraMetricas.Percentil(tempos, 50))} | "
                + $"{Seg(CalculadoraMetricas.Percentil(tempos, 95))} | {Inteiro(CalculadoraMetricas.Media(entradas))} | "
                + $"{Inteiro(CalculadoraMetricas.Media(saidas))} | {Inteiro(CalculadoraMetricas.Media(raciocinios))} | "
                + $"{Inteiro(Soma(entradas))} | {Inteiro(Soma(saidas))} |");
        }

        texto.AppendLine();
    }

    private void EscreverLinhaTempos(string nome, List<double> tempos)
    {
        texto.AppendLine($"| {nome} | {Ms(CalculadoraMetricas.Media(tempos))} | {Ms(CalculadoraMetricas.Percentil(tempos, 50))} | {Ms(CalculadoraMetricas.Percentil(tempos, 95))} |");
    }

    /// <summary>
    /// A base da análise de erros (Passo 6): quando a resposta com RAG estiver errada, "nos trechos =
    /// não" é falha de recuperação; "sim" é falha de geração.
    /// </summary>
    private void EscreverPorPergunta()
    {
        texto.AppendLine("## Por pergunta");
        texto.AppendLine();
        texto.AppendLine("Posição do chunk de referência em cada estratégia (— = fora do top 50); se ele estava entre os trechos que a geração com RAG recebeu; e se a resposta o citou.");
        texto.AppendLine();

        var cabecalho = new StringBuilder("| Id | Tipo |");
        var separador = new StringBuilder("| --- | --- |");
        foreach (string estrategia in estrategias)
        {
            cabecalho.Append($" {estrategia} |");
            separador.Append(" ---: |");
        }

        cabecalho.Append(" Nos trechos | Citou a referência |");
        separador.Append(" --- | --- |");
        texto.AppendLine(cabecalho.ToString());
        texto.AppendLine(separador.ToString());

        foreach (RegistroPergunta registro in respondiveis)
        {
            var linha = new StringBuilder($"| {registro.Id} | {registro.Tipo} |");
            foreach (string estrategia in estrategias)
            {
                int? posicao = CalculadoraMetricas.Posicao(registro, estrategia);
                linha.Append(posicao is null ? " — |" : $" {posicao} |");
            }

            bool isNosTrechos = false;
            bool isCitou = false;
            foreach (RegistroGeracao geracao in registro.Geracoes)
            {
                if (geracao.Modo != RegistroGeracao.ModoComRag || geracao.Rodada != 1)
                {
                    continue;
                }

                foreach (RegistroTrecho trecho in geracao.Trechos)
                {
                    if (trecho.IsReferencia)
                    {
                        isNosTrechos = true;
                    }
                }

                foreach (Guid chunkId in geracao.ChunksCitados)
                {
                    if (registro.ChunksReferencia.Contains(chunkId))
                    {
                        isCitou = true;
                    }
                }
            }

            linha.Append($" {(isNosTrechos ? "sim" : "não")} | {(isCitou ? "sim" : "não")} |");
            texto.AppendLine(linha.ToString());
        }

        texto.AppendLine();
    }

    private void EscreverNotas()
    {
        texto.AppendLine("## Notas");
        texto.AppendLine();
        texto.AppendLine("- Hit@k e MRR contam acerto quando qualquer um dos chunks de referência aparece; o gabarito inclui chunks vizinhos que repetem parte da resposta.");
        texto.AppendLine("- Pelo mesmo motivo, o recall por chunk é pessimista: citar só um de dois chunks redundantes conta como metade.");
        texto.AppendLine("- Com RAG, as páginas e os livros citados vêm dos chunks citados. Sem RAG, saem do texto por regex (\"(Livro do Jogador, p. 95)\"); conferir à mão.");
        texto.AppendLine("- As perguntas sem resposta nos livros ficam fora destas métricas: a recusa é avaliada no julgamento manual (Passo 5).");
    }

    private List<int?> Posicoes(List<RegistroPergunta> perguntas, string estrategia)
    {
        var posicoes = new List<int?>();
        foreach (RegistroPergunta registro in perguntas)
        {
            posicoes.Add(CalculadoraMetricas.Posicao(registro, estrategia));
        }

        return posicoes;
    }

    private List<RegistroPergunta> DoTipo(string tipo)
    {
        var doTipo = new List<RegistroPergunta>();
        foreach (RegistroPergunta registro in respondiveis)
        {
            if (registro.Tipo == tipo)
            {
                doTipo.Add(registro);
            }
        }

        return doTipo;
    }

    private static double Soma(List<double> valores)
    {
        double soma = 0;
        foreach (double valor in valores)
        {
            soma += valor;
        }

        return soma;
    }

    private static string Pct(double valor)
    {
        return valor.ToString("P1", Cultura);
    }

    private static string Dec(double valor)
    {
        return valor.ToString("F3", Cultura);
    }

    private static string Ms(double valor)
    {
        return valor.ToString("N0", Cultura);
    }

    private static string Seg(double valor)
    {
        return valor.ToString("F1", Cultura);
    }

    private static string Inteiro(double valor)
    {
        return valor.ToString("N0", Cultura);
    }
}
