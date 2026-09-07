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

Buscar:

```bash
dotnet run --project Codex20.Embeddings.Playground -c Release -- monstro --buscar "pontos de vida do aarakocra" --top 5
```

| Flag | O que faz |
| --- | --- |
| `--lote N` | chunks por chamada à API (default 32) |
| `--limite N` | só os N primeiros chunks; o livro fica só com eles |
| `--buscar "txt"` | não grava; lista os chunks mais parecidos. Busca no banco inteiro — o livro informado é ignorado |
| `--top N` | resultados do `--buscar` (default 5) |
