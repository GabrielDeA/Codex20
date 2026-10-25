# Codex20.Avaliacao

Roda o gabarito de perguntas por tudo o que o trabalho compara e grava o resultado em JSONL:

```
para cada pergunta do CSV:
  cosseno, bm25, rrf  → posição do 1º chunk de referência (até a posição 50)
  com RAG             → PipelineRag com RRF e 8 trechos (a mesma configuração da Api)
  sem RAG             → PipelineRag.ResponderSemRagAsync: mesmo modelo, sem busca e sem trechos
```

As métricas (Hit@k, MRR, P/R/F1) são calculadas depois, a partir do JSONL.

## O CSV de perguntas

Default: `Codex20.Avaliacao/Dados/perguntas.csv` (outro caminho com `--perguntas`). Exemplo em
`Dados/perguntas.exemplo.csv`. O separador é **ponto e vírgula** (`;`), o padrão do Excel em
português; célula que tenha `;` no texto vai entre aspas (o Excel faz isso sozinho). Salve como
**CSV UTF-8**.

| Coluna | Formato |
| --- | --- |
| `id` | único, ex. `q01` |
| `pergunta` | texto livre |
| `tipo` | `nominal`, `parafrase` ou `combinacao` (acento tanto faz) |
| `livro` | texto livre, ex. `jogador`, `jogador+mestre` (só para recortes) |
| `resposta_referencia` | texto livre, escrito a partir do livro (para o julgamento manual) |
| `paginas_referencia` | `livro:pagina` ou `livro:inicio-fim`, vários separados por espaço: `jogador:95 mestre:12-13` |
| `chunks_referencia` | `Id` dos chunks que trazem a resposta, separados por espaço. Qualquer um deles entre os recuperados conta como acerto |
| `respondivel` | `sim` ou `nao`. As `nao` ficam sem chunks e testam a recusa |

### Achando os Ids sem usar a busca

Montar o gabarito com a busca do próprio sistema favoreceria o sistema. Estes dois comandos só
filtram os `*.chunks.json`, sem ranquear:

```bash
dotnet run --project Codex20.Avaliacao -- pagina jogador 95
```

```bash
dotnet run --project Codex20.Avaliacao -- localizar "espaços de magia" --livro jogador
```

### Conferindo o gabarito

```bash
dotnet run --project Codex20.Avaliacao -- validar
```

Não chama a API. É erro (impede o `rodar`): Id que não existe nos `*.chunks.json` atuais,
pergunta respondível sem chunk, id repetido, campo fora do formato. É aviso: chunk fora das páginas
de referência (Id colado da linha errada?), tipo desconhecido.

O `Chunk.Id` é hash do texto: **qualquer mudança no chunking invalida o gabarito**. O `validar`
pega isso, mas o certo é congelar o código antes de escrever o gabarito.

## Rodando

Usa os mesmos user-secrets do Painel (`AzureOpenAI:Endpoint`, `Key`, `DeploymentEmbedding`,
`DeploymentChat` e o opcional `EsforcoRaciocinio`).

```bash
dotnet run --project Codex20.Avaliacao -- rodar
```

| Opção | Para quê |
| --- | --- |
| `--ids q01,q02` | só algumas perguntas (teste barato) |
| `--repeticoes 3` | gera cada resposta 3 vezes, com e sem RAG, para medir a variação |
| `--profundidade 50` | até que posição procurar o chunk de referência |
| `--continuar ARQUIVO` | retoma um JSONL interrompido, pulando as perguntas que ele já tem. Recusa se o modelo configurado mudou |

Grava em `Codex20.Avaliacao/Resultados/`:

- `avaliacao-<data>.jsonl`: uma linha por pergunta, gravada assim que ela termina.
- `avaliacao-<data>.meta.json`: modelo, modelo de embedding, parâmetros e os dois prompts inteiros.

## O que vai em cada linha do JSONL

- `Recuperacao[]`, uma por estratégia: `PosicaoPrimeiraReferencia` (null = fora da profundidade,
  ou pergunta sem resposta), a posição de cada chunk de referência e o top 8 com `IsReferencia`.
- `Geracoes[]`, uma `com-rag` e uma `sem-rag` por rodada: `Texto`, os trechos que o modelo viu,
  `Citacoes` [n] e os `ChunksCitados`, `PaginasCitadas` e `LivrosCitados`, tokens, tempos e se
  cortou.

O texto dos chunks não vai no JSONL: ele está no `*.chunks.json`, pelo Id.

**Fontes sem RAG.** O prompt pede `(Livro do Jogador, p. 95)` ou só `(Livro do Jogador)` quando o
modelo não lembra a página. `PaginasCitadas` e `LivrosCitados` saem desse texto por regex; confira
à mão, o modelo nem sempre segue o formato. Com RAG elas vêm das páginas dos chunks citados, o que
permite comparar os dois modos por livro e página.
