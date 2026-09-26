# Codex20.Geracao

O "G" do RAG: recebe os trechos que a busca recuperou e pede a um modelo de linguagem a resposta,
citando cada trecho como `[n]`.

```
pergunta → [busca: cosseno | bm25 | rrf] → trechos → [MontadorPrompt] → [modelo] → resposta com [n]
```

| Onde | O quê |
| --- | --- |
| `Codex20.Core/Geracao` | `PipelineRag` (busca → prompt → modelo → confere citações), `MontadorPrompt`, `LeitorCitacoes`, `IGeradorResposta` |
| `Codex20.Geracao` | `GeradorRespostaAzureOpenAI`: a única classe que fala com a API |
| `Codex20.Painel` | tela **Perguntar** |

## Configuração (uma vez)

1. No [Microsoft Foundry](https://ai.azure.com), no mesmo recurso dos embeddings, crie um deployment
   do modelo (ex. `gpt-5.6-luna`, tipo Global Standard). O nome do deployment é o que vai abaixo.
2. Configure (mesmos user-secrets do Painel e do Embeddings.Playground; Endpoint e Key já existem):

```bash
dotnet user-secrets set "AzureOpenAI:DeploymentChat" "gpt-5.6-luna" --project Codex20.Painel
dotnet user-secrets set "AzureOpenAI:EsforcoRaciocinio" "low" --project Codex20.Painel
```

`EsforcoRaciocinio` é opcional (`minimal`, `low`, `medium`, `high`) e só vale para modelos de
raciocínio (família GPT-5). **Apague-o ao usar um modelo sem raciocínio** (ex. DeepSeek V4 Flash) —
mandar o parâmetro a um modelo que não o aceita dá erro:

```bash
dotnet user-secrets remove "AzureOpenAI:EsforcoRaciocinio" --project Codex20.Painel
```

## Trocar de modelo

Crie o deployment no Foundry e troque `AzureOpenAI:DeploymentChat`. Nada no código muda: o
gerador usa o SDK da OpenAI na API v1 da Azure (`<endpoint>/openai/v1/`) e a API de Chat
Completions, que os modelos de terceiros do Foundry também atendem.

Preço de referência (Global Standard, US$ por 1M tokens, agosto/2026):

| Modelo | Entrada | Saída |
| --- | --- | --- |
| gpt-5.6-luna | 0,20 | 1,20 |
| gpt-5.6-terra | 2,00 | 12,00 |
| DeepSeek-V4-Flash | 0,19 | 0,51 |
| Kimi-K2.6 (preview) | 0,95 | 4,00 |

Os tokens de raciocínio são cobrados como saída. A tela mostra entrada, saída e quanto da saída foi
raciocínio em cada pergunta.

## Decisões

- **Modelo não chamado sem trecho.** Se a busca não traz nada, o `PipelineRag` devolve só o aviso:
  sem contexto, o modelo responderia com o próprio conhecimento, que é justamente o que as
  instruções proíbem.
- **Uma pergunta por vez, sem histórico.** Conversa com memória exige reescrever a pergunta antes
  da busca ("e os pontos de vida *dele*?").
- **Citação conferida.** Número citado que não existe no prompt (ex. `[9]` com 8 trechos) aparece
  em vermelho e vira aviso: é citação inventada.
- **Sem `temperature`.** Os modelos de raciocínio só aceitam o valor padrão.
- **Teto de 4.096 tokens de saída**, raciocínio incluído. Se o modelo bater nele, a tela avisa que a
  resposta pode estar incompleta.
