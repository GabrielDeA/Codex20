# Codex20.Embeddings

Transforma os chunks em vetores e guarda num vector store SQLite.

```
<livro>.chunks.json → [Embeddings.Playground] → embeddings.db
```

## Configuração (uma vez)

```bash
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://<seu-recurso>.openai.azure.com/" --project Codex20.Embeddings.Playground
dotnet user-secrets set "AzureOpenAI:Key" "<sua-chave>" --project Codex20.Embeddings.Playground
dotnet user-secrets set "AzureOpenAI:DeploymentEmbedding" "text-embedding-3-large" --project Codex20.Embeddings.Playground
```

Também funciona por variável de ambiente: `AzureOpenAI__Endpoint`, `AzureOpenAI__Key`,
`AzureOpenAI__DeploymentEmbedding`.

O deployment precisa bater com `ArmazenamentoVetorialSqlite.Dimensoes` (3072 =
`text-embedding-3-large`, 1536 = `text-embedding-3-small`). Trocar exige recriar o banco.

## Uso

Gerar o JSON de chunks:

```bash
dotnet run --project Codex20.Chunking.Playground -c Release -- <monstro|jogador|mestre> --save
```

Gerar os embeddings:

```bash
dotnet run --project Codex20.Embeddings.Playground -c Release -- <monstro|jogador|mestre>
```

Montar o índice BM25 (não chama a API; rode para cada livro, ou use o botão na tela Embeddings do Painel):

```bash
dotnet run --project Codex20.Embeddings.Playground -c Release -- monstro --indexar-bm25
```

Buscar:

```bash
dotnet run --project Codex20.Embeddings.Playground -c Release -- monstro --buscar "pontos de vida do aarakocra" --top 5
```

Comparar as estratégias (imprime uma tabela Markdown):

```bash
dotnet run --project Codex20.Embeddings.Playground -c Release -- monstro --buscar "pontos de vida do aarakocra" --comparar
```

| Flag | O que faz |
| --- | --- |
| `--lote N` | chunks por chamada à API (default 32) |
| `--limite N` | só os N primeiros chunks; o livro fica só com eles |
| `--indexar-bm25` | não gera vetores; monta o índice BM25 do livro. Não precisa de credenciais |
| `--compactar` | reescreve o banco sem o espaço morto da vec0. Gerar embeddings já compacta sozinho; use para bancos antigos. O livro informado é ignorado |
| `--buscar "txt"` | não grava; lista os chunks mais parecidos. Busca no banco inteiro — o livro informado é ignorado |
| `--estrategia NOME` | `cosseno`, `bm25` ou `rrf` (default `cosseno`) |
| `--comparar` | com `--buscar`, roda as três e imprime os rankings lado a lado com o tempo de cada uma |
| `--top N` | resultados do `--buscar` (default 5) |

## Estratégias de busca

As três implementam `IEstrategiaBusca` (`Codex20.Core/Busca`). Uma 4ª entra criando a classe e
acrescentando-a em `ServicoBusca.MontarEstrategias` (Painel) e na montagem do Playground.

| Estratégia | Como ranqueia | Pontuação |
| --- | --- | --- |
| `cosseno` | embeda a consulta e busca os vizinhos na `vec0` do sqlite-vec | similaridade de cosseno, 1 = idêntico |
| `bm25` | índice invertido do FTS5 (tabela `chunks_fts`, no mesmo `.db`) | `-bm25()`: relativo ao corpus, maior = melhor |
| `rrf` | Reciprocal Rank Fusion das duas: `Σ 1/(60 + posição)`, com 50 candidatos por perna | ~0,0x, maior = melhor |

Pontuações de estratégias diferentes **não se comparam** — só a ordem dentro de cada uma importa.
O tempo do `cosseno` (e do `rrf`, que roda as duas pernas) inclui a chamada à API de embeddings,
que domina a latência; a primeira consulta também paga o cache frio do banco.

**Limitações do BM25.** O tokenizer é `unicode61 remove_diacritics 2`: ignora maiúsculas e acentos,
mas **não há stemming de português** — "magia" e "magias" são termos distintos. O `porter` do FTS5
foi descartado por ser stemmer de inglês. A consulta é reduzida às palavras e unida por `OR`, então
operadores do FTS5 digitados pelo usuário viram texto comum.

**Por que não há HNSW/ANN.** O `sqlite-vec` 0.1.7-alpha não implementa índice aproximado: a `vec0`
faz KNN exaustivo, comparando a consulta com todos os vetores. O suporte a ANN (IVF, HNSW, DiskANN)
está em aberto na [issue #25](https://github.com/asg017/sqlite-vec/issues/25) desde 2024. Com
3.176 chunks × 3.072 dimensões a busca exaustiva responde em milissegundos, então um índice
aproximado não traria ganho mensurável nessa escala — e a exaustiva tem recall exato.
