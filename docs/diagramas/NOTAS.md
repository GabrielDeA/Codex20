# Notas dos diagramas da seção 3.1

Rascunhos para redesenho manual. Cada figura tem um `.puml` (fonte) e um `.svg` (renderizado).

| Figura | Fonte | Renderizado |
| --- | --- | --- |
| 2 – Fluxos de preparação e de consulta | `figura2-fluxos.puml` | `figura2-fluxos.svg` |
| 3 – Casos de uso | `figura3-casos-de-uso.puml` | `figura3-casos-de-uso.svg` |
| 4 – Classes do Codex20.Core | `figura4-classes-core.puml` | `figura4-classes-core.svg` |
| 5 – Sequência de uma consulta com RRF | `figura5-sequencia-rrf.puml` | `figura5-sequencia-rrf.svg` |
| 4 (versão compacta) | `figura4-classes-core-compacta.puml` | `figura4-classes-core-compacta.svg` |
| 4 (versão vertical, para o apêndice) | `figura4-classes-core-vertical.puml` | `figura4-classes-core-vertical.svg` |
| 5 (versão simplificada) | `figura5-sequencia-rrf-simplificada.puml` | `figura5-sequencia-rrf-simplificada.svg` |

As versões compacta e simplificada (2026-10-17) e a vertical (2026-10-18) são alternativas às
originais, que continuam na pasta sem alteração. Ver as seções "Versão compacta", "Versão
vertical" e "Versão simplificada" abaixo.

Renderizado com PlantUML 1.2026.8 (jar do Maven Central). As figuras 2, 3 e 4 usam
`!pragma layout smetana`, o motor de layout embutido no próprio jar, então não é preciso
instalar o Graphviz. Para regerar, rode dentro de `docs/diagramas`:

```bash
java -jar plantuml.jar -charset UTF-8 -tsvg *.puml
```

Atualizado em 2026-10-12 para o commit `410daa5`, que criou o `Codex20.App` e implementou a
`Codex20.Api`. O cliente do usuário final é um **webApp**, não um aplicativo mobile: um PWA em
Blazor WebAssembly, instalável pelo navegador do celular. Nenhum componente das figuras é mais
planejado, então a marcação `<<planejado>>` saiu de todas.

A solução tem três projetos novos ou alterados para isso:

| Projeto | Papel |
| --- | --- |
| `Codex20.App` | webApp (PWA, Blazor WebAssembly) com uma tela só: pergunta, resposta e fontes. Roda no navegador de quem usa e só referencia o `Codex20.Contratos` |
| `Codex20.Api` | `POST /api/consultas`. Roda o RAG (sempre RRF, 8 trechos) e também serve os arquivos do webApp pela mesma URL |
| `Codex20.Contratos` | DTOs trocados entre os dois (`PedidoConsulta`, `RespostaConsulta`, `ParteResposta`, `FonteConsulta`), sem nenhuma dependência |

---

## Resposta: o RRF executa as buscas em paralelo ou em sequência?

**Em sequência.** Em `EstrategiaBuscaRrf.BuscarAsync`
([EstrategiaBuscaRrf.cs:101-103](../../Codex20.Core/Busca/EstrategiaBuscaRrf.cs#L101)), as
pernas são percorridas com `foreach` e cada uma é aguardada (`await perna.BuscarAsync(...)`)
antes de a próxima começar. Não há `Task.WhenAll` nem nada equivalente. Por isso a Figura 5 não
usa `par`: a perna de cosseno roda inteira (embedding da pergunta + vec0) e só depois a de BM25
(FTS5).

A ordem é a da lista montada por quem cria a fusão: primeiro cosseno, depois BM25. A Api monta
essa lista no construtor do `ServicoConsulta`
([ServicoConsulta.cs:35-37](../../Codex20.Api/Servicos/ServicoConsulta.cs#L35)), e o Painel em
`ServicoBusca.MontarEstrategias`
([ServicoBusca.cs:62-64](../../Codex20.Painel/Servicos/ServicoBusca.cs#L62)). O `ServicoBusca`
também roda a comparação de estratégias em sequência
(`CompararAsync`), e o comentário no código diz por quê: em paralelo, as estratégias
disputariam CPU e o arquivo do banco, e os tempos medidos não significariam nada.

---

## Figura 2 – Fluxos de preparação e de consulta

Diagrama no estilo de componentes, com uma coluna para cada fluxo. Os artefatos (arquivos)
aparecem como nós amarelos, e cada etapa fica dentro de um quadro com o projeto que a executa.

### Origem dos elementos

| Elemento | Arquivo / classe |
| --- | --- |
| Extração de layout (Document Intelligence, `prebuilt-layout`, saída Markdown) | `Codex20.Ingestion/DocumentIntelligenceService.cs` – `DocumentIntelligenceService.LerDocumentoAsync`; chamado por `Codex20.Ingestion/Program.cs` |
| `Data/Markdown/<nome>.md` | Gravado por `LerDocumentoAsync` (o `<nome>` vem do nome do PDF). Também grava `Data/Json/<nome>.json` com o `AnalyzeResult` bruto, que ficou fora da figura |
| `Data/Markdown/RESULTADO_<livro>.md` | Nomes definidos em `Codex20.Painel/Servicos/CatalogoLivros.cs` – `ArquivoMarkdown`, e em `ResolverCaminhoPadrao` do `Codex20.Chunking.Playground/Program.cs` |
| Pré-processamento | `Codex20.Core/PreProcessamento/PreProcessadorDocumentoMarkdown.cs` – `Processar` (gera `List<BlocoDocumento>`) |
| Fragmentação | `Codex20.Core/Chunking/ChunkingStrategyPorEntidade.cs` (com `ChunkingStrategyParagrafoToken` como fallback) |
| Quem executa o pré-processamento e a fragmentação | `Codex20.Chunking.Playground/Program.cs` (opção `--save`) ou `Codex20.Painel/Servicos/ServicoChunking.cs` (tela Chunking) |
| `Data/Chunks/<livro>.chunks.json` | `Codex20.Core/Chunking/ArquivoChunks.cs` – `Salvar` / `Ler` |
| Geração de embeddings | `Codex20.Embeddings/GeradorEmbeddingAzureOpenAI.cs` (API) + `Codex20.Embeddings/ArmazenamentoVetorialSqlite.cs` (`SalvarAsync`) |
| Indexação BM25 | `Codex20.Embeddings/IndiceTextualFts5.cs` – `IndexarAsync` |
| Quem executa embeddings e indexação | `Codex20.Embeddings.Playground/Program.cs` (padrão e `--indexar-bm25`) ou `Codex20.Painel/Servicos/ServicoEmbeddings.cs` (tela Embeddings) |
| `embeddings.db`: tabela `chunks` (vec0) e `chunks_fts` (FTS5) | Caminho em `CaminhosDados.Banco()`; nomes das tabelas em `ServicoEmbeddings.cs:31-33`; DDL em `ArmazenamentoVetorialSqlite.cs:79-81` e `IndiceTextualFts5.cs:40` |
| Busca (cosseno, BM25 ou RRF) | `Codex20.Core/Busca/IEstrategiaBusca.cs` e as três implementações |
| Montagem do prompt | `Codex20.Core/Geracao/MontadorPrompt.cs` |
| Geração da resposta | `Codex20.Geracao/GeradorRespostaAzureOpenAI.cs` |
| Leitura das citações | `Codex20.Core/Geracao/LeitorCitacoes.cs` |
| Orquestração da consulta | `Codex20.Core/Geracao/PipelineRag.cs` – `ResponderAsync` |
| `RespostaGerada` (saída do pipeline) | `Codex20.Core/Geracao/RespostaGerada.cs` |
| Codex20.App (webApp) | `Codex20.App/Pages/Home.razor` → `Codex20.App/Servicos/ClienteConsulta.cs` – `ConsultarAsync` |
| `POST /api/consultas` (`PedidoConsulta`) | `ClienteConsulta.cs:51`; `Codex20.Api/Controllers/ConsultasController.cs` – `Consultar` (rota na linha 11) |
| Recepção da pergunta | `ConsultasController.Consultar` → `Codex20.Api/Servicos/ServicoConsulta.cs` – `ConsultarAsync` (linha 81 chama `PipelineRag.ResponderAsync` com a estratégia RRF) |
| Conversão da resposta | `Codex20.Api/Servicos/ConversorResposta.cs` – `Converter` |
| `RespostaConsulta` (JSON para o webApp) | `Codex20.Contratos/RespostaConsulta.cs`, com `ParteResposta` e `FonteConsulta` |
| Codex20.Painel (tela Perguntar) | `Codex20.Painel/Components/Pages/Perguntar.razor` → `Codex20.Painel/Servicos/ServicoGeracao.cs` – `ResponderAsync`. Exibe a `RespostaGerada` direto, com o texto dos trechos |

### Divergências em relação à descrição

- **"Geração de embeddings → embeddings.db (vec0 + FTS5)" são duas etapas independentes.**
  A geração de embeddings chama a API e grava só a tabela vec0 `chunks`. A indexação BM25 não
  chama a API e grava só a tabela FTS5 `chunks_fts`. As duas leem o mesmo `.chunks.json` e
  gravam no mesmo arquivo, mas rodam separadas: são comandos diferentes no Playground e botões
  diferentes no Painel. A figura mostra as duas como etapas paralelas no mesmo quadro.
- **A revisão manual não tem código.** O Ingestion grava `Data/Markdown/<nome-do-pdf>.md`, e os
  consumidores leem `RESULTADO_<livro>.md`. O único registro do passo é o texto de ajuda do
  Chunking.Playground, que chama esse arquivo de "RESULTADO_*.md revisado"
  (`Program.cs:12` e `114`).
- **O `Codex20.Core` não executa nada sozinho.** É uma biblioteca, e quem roda as etapas são o
  Chunking.Playground, o Embeddings.Playground e o Painel. A figura mostra isso no título de
  cada quadro.
- **O fluxo de consulta tem duas entradas.** O webApp chama a Api, que sempre usa o RRF com 8
  trechos. O Painel chama o `PipelineRag` direto, pela tela Perguntar
  ([ServicoGeracao.cs:60](../../Codex20.Painel/Servicos/ServicoGeracao.cs#L60)), e deixa
  escolher a estratégia e a quantidade de trechos. As duas entradas usam o mesmo `PipelineRag` e
  o mesmo banco.
- **A Api aparece duas vezes na coluna da consulta.** Ela recebe a pergunta e, no fim, converte
  a `RespostaGerada` em `RespostaConsulta` (`ConversorResposta`). É nessa conversão que o texto
  dos trechos fica para trás: para o webApp só vão livro, páginas e nome da entidade, por
  questão de propriedade intelectual (comentário de `RespostaConsulta`). As fontes são
  renumeradas de 1 em diante, na ordem da primeira citação.
- **A Api também serve o webApp.** Os arquivos do Blazor WebAssembly saem pela mesma URL
  (`MapStaticAssets` e `MapFallbackToFile` em
  [Program.cs:61-73](../../Codex20.Api/Program.cs#L61)), e o `HttpClient` do webApp usa o
  endereço de onde a página veio (`Codex20.App/Program.cs:11`). Para os testadores, a Api roda
  na máquina de desenvolvimento e é exposta pelo Tailscale Funnel, que entrega o HTTPS num
  endereço fixo. O Funnel não aparece na figura nem no código; antes dele foi usado o ngrok, que
  ainda aparece em comentários e num header do `ClienteConsulta` (resumo técnico, seção 7.1).

---

## Figura 3 – Casos de uso

### Origem dos elementos

| Caso de uso | Requisito | Onde existe hoje |
| --- | --- | --- |
| Fazer pergunta e receber resposta com as fontes (livro e página) | RF01, RF03, RF05 | webApp: `Home.razor` → `ClienteConsulta` → `POST /api/consultas` → `ServicoConsulta` → `PipelineRag`. Fontes: cada citação `[n]` do modelo aponta para um trecho (`LeitorCitacoes`, `RespostaGerada.Citacoes`); o `ConversorResposta` transforma as citadas na lista "Fontes", com livro, páginas e entidade, e a `Home.razor` mostra a citação como número sobrescrito ligado a essa lista |
| Processar livro | RF06 | Inclui os três abaixo |
| Converter PDF em Markdown | RF06 | `Codex20.Ingestion` (linha de comando) |
| Gerar trechos (chunking) | RF06 | Tela Chunking (`ServicoChunking`) ou `Codex20.Chunking.Playground` |
| Gerar embeddings e índice BM25 | RF06 | Tela Embeddings (`ServicoEmbeddings`) ou `Codex20.Embeddings.Playground` |
| Comparar estratégias de busca | RF07 | Tela Busca, opção "Comparar todas" (`ServicoBusca.CompararAsync`); também `--comparar` no Embeddings.Playground |

RF02 e RF04 aparecem como nota de restrição ligada a "Fazer pergunta". No código, as duas são
regras escritas nas instruções fixas do prompt (`MontadorPrompt.Instrucoes`). O RF02 também é
reforçado em `PipelineRag.ResponderAsync`
([PipelineRag.cs:36](../../Codex20.Core/Geracao/PipelineRag.cs#L36)): sem nenhum trecho, o
modelo não é chamado. Nesse caso, o webApp mostra o aviso "Não encontrei nada nos livros sobre
isso" (`ConversorResposta.MontarAviso`, linha 76), o que também atende o RF04.

### Decisões e divergências

- **Associações sem rótulo e sem seta**, como é o padrão para ligar ator e caso de uso. O canal
  de cada ator fica para o texto do artigo:
  - o usuário final usa o webApp (`Codex20.App`);
  - o administrador usa o painel;
  - "Converter PDF em Markdown" é a exceção: só existe como linha de comando
    (`Codex20.Ingestion`), fora do painel.
- **O Administrador não está ligado a "Fazer pergunta".** Hoje a tela Perguntar do painel é a
  única forma de perguntar, mas no sistema final ela é ferramenta de teste, e o caso de uso
  pertence ao usuário final.
- **RF05 foi reescrito.** Antes era "exibir os trechos usados"; agora é "exibir as fontes
  usadas: livro e página". O webApp nunca mostra o texto dos trechos, por decisão de propriedade
  intelectual: a Api devolve só livro, páginas e nome da entidade (`RespostaConsulta`,
  `ConversorResposta`). O texto dos trechos continua visível só no Painel, como ferramenta do
  administrador.
- **RF03 e RF05 estão dentro do caso principal, não em casos próprios.** As fontes fazem parte
  da resposta: elas chegam junto, na mesma tela, sem ação extra do usuário. Um caso "Ver livro e
  página" separado seria decomposição funcional, porque ninguém faz isso como objetivo próprio.
  Ele também tiraria a resposta do diagrama. Por isso o nome do caso principal diz "receber
  resposta com as fontes".
- **A entrada com código de acesso não virou caso de uso.** O webApp pede um código (ou recebe
  pelo link de convite `/?chave=`) antes de liberar a pergunta (`Home.razor`,
  `ClienteConsulta`). É uma proteção de custo da fase de testes, não um objetivo do usuário.
- A revisão manual do Markdown é tarefa humana, não do sistema, e por isso não virou caso de uso.

---

## Figura 4 – Classes do Codex20.Core

### Origem dos elementos

| Classe / interface | Arquivo |
| --- | --- |
| `IChunkingStrategy` | `Codex20.Core/Chunking/IChunkingStrategy.cs` |
| `ChunkingStrategyPorEntidade` | `Codex20.Core/Chunking/ChunkingStrategyPorEntidade.cs` |
| `ChunkingStrategyParagrafoToken` | `Codex20.Core/Chunking/ChunkingStrategyParagrafoToken.cs` |
| `IRegrasEntidade` | `Codex20.Core/Chunking/IRegrasEntidade.cs` |
| `RegrasEntidadeMonstro`, `RegrasEntidadeMagia`, `RegrasEntidadeItemMagico` | `Codex20.Core/Chunking/RegrasEntidade/*.cs` |
| `Chunk` | `Codex20.Core/Chunking/Chunk.cs` |
| `IEstrategiaBusca` | `Codex20.Core/Busca/IEstrategiaBusca.cs` |
| `EstrategiaBuscaCosseno`, `EstrategiaBuscaBm25`, `EstrategiaBuscaRrf` | `Codex20.Core/Busca/EstrategiaBusca*.cs` |
| `IIndiceTextual` | `Codex20.Core/Busca/IIndiceTextual.cs` |
| `IGeradorEmbedding`, `IArmazenamentoVetorial` | `Codex20.Core/Embeddings/*.cs` |
| `PipelineRag`, `MontadorPrompt`, `LeitorCitacoes`, `IGeradorResposta` | `Codex20.Core/Geracao/*.cs` |
| `GeradorEmbeddingAzureOpenAI`, `ArmazenamentoVetorialSqlite`, `IndiceTextualFts5` | `Codex20.Embeddings/*.cs` |
| `GeradorRespostaAzureOpenAI` | `Codex20.Geracao/GeradorRespostaAzureOpenAI.cs` |

### Divergências em relação à descrição

- **O `PipelineRag` não usa `IGeradorEmbedding` nem `IArmazenamentoVetorial`.** O único campo
  dele é `IGeradorResposta gerador`
  ([PipelineRag.cs:12](../../Codex20.Core/Geracao/PipelineRag.cs#L12)). A estratégia de busca
  chega como parâmetro de `ResponderAsync(IEstrategiaBusca busca, PedidoResposta pedido)`, para
  a tela poder comparar respostas entre estratégias. Por isso a relação com `IEstrategiaBusca` é
  dependência (`..>`, "parâmetro"), não composição. `IGeradorEmbedding` e
  `IArmazenamentoVetorial` são usados pela `EstrategiaBuscaCosseno`, e a figura liga essas
  interfaces a ela.
- **Interfaces fora da lista que entraram na figura:**
  - `IGeradorResposta`: é "o que mais o PipelineRag usa";
  - `IIndiceTextual`: é a interface do FTS5, usada pela `EstrategiaBuscaBm25`;
  - `IChunkingStrategy`: é implementada pelas duas estratégias de chunking. Sem ela, a relação
    real de implementação some.
- **`MontadorPrompt` e `LeitorCitacoes` são `static class`.** O `PipelineRag` chama métodos
  estáticos e não guarda instância, então a relação é dependência ("usa") com o estereótipo
  `<<static>>`.
- **Composição ou agregação: o código não distingue.** Toda referência é um campo `readonly`
  recebido no construtor. O critério usado foi:
  - **composição (◆)** em `ChunkingStrategyPorEntidade` → `IRegrasEntidade`. As regras são
    criadas pelas fábricas estáticas (`ParaManualDosMonstros`, `ParaLivroDoJogador`,
    `ParaGuiaDoMestre`) e não são compartilhadas. O construtor, porém, é público e aceita
    qualquer `IRegrasEntidade`;
  - **agregação (◇)** onde a instância vem de fora:
    - o fallback, criado por `CatalogoLivros.CriarStrategy` e pelo Chunking.Playground;
    - as pernas do RRF, que são as mesmas instâncias de cosseno e BM25 listadas sozinhas no
      `ServicoBusca`;
    - o gerador e o armazenamento das estratégias.
- **O fallback é tipado pela classe concreta**, `ChunkingStrategyParagrafoToken`, e não por
  `IChunkingStrategy`, porque o código usa `ChunkFaixa`, que não está na interface.
- **O RRF não conhece cosseno nem BM25 pelo tipo.** Recebe `List<IEstrategiaBusca> pernas`, por
  isso a agregação é com a interface, com multiplicidade `*`. A ligação concreta às duas pernas
  acontece fora do Core: na Api (`ServicoConsulta`, construtor) e no Painel
  (`ServicoBusca.MontarEstrategias`).
- **O Codex20.App e a Codex20.Api não entram nesta figura**, porque ela é do Core. As classes
  deles aparecem na Figura 5 (`ClienteConsulta`, `ConsultasController`, `ServicoConsulta`).
- **As três classes de regras são `internal`**, marcadas com `<<internal>>`. Fora do Core, só
  as fábricas de `ChunkingStrategyPorEntidade` as instanciam.
- **`Chunk.Id` é uma propriedade calculada** (SHA-256 de `Livro + "\n" + Texto`, truncado em 16
  bytes), não um campo gravado. É a chave da fusão no RRF.
- Os tipos de dados que aparecem só nas assinaturas (`PedidoBusca`, `RespostaBusca`,
  `ResultadoBusca`, `PedidoResposta`, `RespostaGerada`, `ConclusaoModelo`, `BlocoDocumento`)
  ficaram fora da figura para ela caber em meia página.

### Versão compacta (`figura4-classes-core-compacta`)

Mesmas classes, membros e relações da original; muda só a organização. A original ocupava
2028×1248 px com muito espaço vazio; a compacta ocupa 1555×1046 px.

- **Duas colunas.** À esquerda, `Geracao` e, embaixo, `Chunking`; à direita, `Busca` e, embaixo,
  `Embeddings`. Quem segura essa posição são duas ligações ocultas (`-[hidden]->`) e a relação
  "parâmetro" escrita com `...>` (um nível mais longa), que desce `Busca` para o lado de
  `Chunking`.
- **Sem o pacote externo "Codex20.Core".** Cada pacote tem o nome completo
  (`Codex20.Core.Busca` etc.), o que tira um nível de moldura.
- **Sem o pacote "Implementações concretas".** As quatro classes de fora do Core ficam logo
  abaixo da interface que implementam, com o projeto como estereótipo
  (`<<Codex20.Embeddings>>`, `<<Codex20.Geracao>>`) e fundo branco para se distinguirem das
  classes do Core. Na original, essas setas atravessavam a figura inteira até o rodapé.

### Versão vertical (`figura4-classes-core-vertical`)

Feita para o apêndice, numa página em pé. Mesmo conteúdo da compacta (classes, membros,
relações e implementações com o projeto como estereótipo), mas com os quatro pacotes empilhados
numa coluna só: `Geracao`, `Busca`, `Embeddings` e `Chunking`. Ocupa 848×1629 px.

- A ordem segue a consulta (o pipeline usa a busca, a busca de cosseno usa os embeddings), e
  `Chunking`, que é da preparação, fica no fim.
- O preço é a dependência "lê livro e página", do `MontadorPrompt` até o `Chunk`, que desce pela
  esquerda da figura inteira.
- Quem segura a coluna são quatro ligações ocultas (`-[hidden]->`) e a relação "parâmetro"
  escrita com `...>`; estão comentadas no fim do `.puml`.

---

## Figura 5 – Sequência de uma consulta com RRF

### Origem dos elementos

| Participante / mensagem | Arquivo / classe |
| --- | --- |
| `:ClienteConsulta` (webApp) | `Codex20.App/Servicos/ClienteConsulta.cs` – `ConsultarAsync`; chamado por `Codex20.App/Pages/Home.razor` |
| `POST /api/consultas`, `PedidoConsulta` + `X-Chave-Acesso` | `ClienteConsulta.cs:51-55`; `Codex20.Contratos/PedidoConsulta.cs` |
| `:ConsultasController` e a nota "Antes de consultar" | `Codex20.Api/Controllers/ConsultasController.cs` – `Consultar`: limite por IP (`[EnableRateLimiting]`, linha 30; 5 por minuto em `Program.cs:35`), chave (linha 33), tamanho de 1 a 500 caracteres (linha 39), limite diário (`ServicoConsulta.ReservarConsulta`, linha 44; 200 por padrão) |
| `:ServicoConsulta` – `ConsultarAsync(pergunta)` | `Codex20.Api/Servicos/ServicoConsulta.cs`, linha 81 |
| `ResponderAsync(rrf, pedido)` | `PipelineRag.ResponderAsync`; o `pedido` é um `PedidoResposta` só com a pergunta, então vale `QuantidadeTrechos = 8` (`PedidoResposta.cs:13`) |
| Quantidade das pernas = max(8, 50) | `EstrategiaBuscaRrf.cs:90` (`CandidatosMinimos = 50`, linha 25) |
| Pernas em sequência | `EstrategiaBuscaRrf.cs:101-103` |
| `GerarEmLoteAsync([pergunta])` → vetor de 3072 dimensões | `EstrategiaBuscaCosseno.BuscarAsync`; dimensão em `ArmazenamentoVetorialSqlite.Dimensoes` |
| `BuscarSimilaresAsync(vetor, 50)` | `ArmazenamentoVetorialSqlite.BuscarSimilaresAsync` (vec0, `distance_metric=cosine`; similaridade = 1 − distância) |
| `BuscarTextoAsync(pergunta, 50)` | `EstrategiaBuscaBm25.BuscarAsync` → `IndiceTextualFts5.BuscarTextoAsync` (ordenado pelo `bm25()` do FTS5) |
| Fusão Σ 1/(60 + posição) por `Chunk.Id` | `EstrategiaBuscaRrf.BuscarAsync` (`KRrf = 60`, linha 18) |
| `alt Resultados.Count == 0` | `PipelineRag.cs:36` |
| `MontarMensagem` / `Instrucoes` | `MontadorPrompt` (`PipelineRag.cs:54`) |
| `GerarAsync(Instrucoes, mensagem)` | `IGeradorResposta`, implementado por `GeradorRespostaAzureOpenAI` (`PipelineRag.cs:57`) |
| `Extrair(texto)` e a separação entre citações válidas e inexistentes | `LeitorCitacoes.Extrair` (`PipelineRag.cs:62` em diante) |
| `ConversorResposta.Converter` | `Codex20.Api/Servicos/ConversorResposta.cs`: renumeração das fontes (linhas 13–41), aviso sem trechos (linha 76), chamado em `ServicoConsulta.cs:94` |
| `200 OK, RespostaConsulta` | `ConsultasController.Consultar`, linha 52; `Codex20.Contratos/RespostaConsulta.cs` |

### Divergências e simplificações

- **O `ConversorResposta` aparece como mensagem para o próprio `ServicoConsulta`**, e não como
  participante. É uma classe estática que o serviço chama, e um participante a mais deixaria a
  figura larga demais para meia página.
- **Os retornos de erro ficaram fora.** O controller responde 401 (chave errada), 400 (tamanho),
  429 (limite por IP ou diário) e 502 (falha no Azure OpenAI), sempre com uma mensagem pronta
  para o webApp mostrar (`ConsultasController.cs:35-58`). A nota "Antes de consultar" resume
  essas verificações.
- **No RRF, o ramo sem trechos é raro.** Ele só acontece se as duas pernas voltarem vazias. A
  de cosseno sempre devolve vizinhos enquanto houver vetores no banco, então pelo webApp o ramo
  só aparece com o banco vazio. Nesse caso, o `PipelineRag` não chama o modelo, e o
  `ConversorResposta` põe o aviso "Não encontrei nada nos livros" na `RespostaConsulta`.
- **Omitido para caber na figura:**
  - o `EstrategiaBuscaBm25` volta sem chamar o FTS5 quando a consulta não tem nenhum termo
    pesquisável (`IsPesquisavel`);
  - o `PipelineRag` cronometra a busca e a geração (`Stopwatch`);
  - a `RespostaGerada` também traz tokens, a mensagem enviada e os avisos (resposta cortada,
    citação inexistente);
  - o `ServicoConsulta` grava no log cada pergunta, com trechos citados, tokens e tempo
    (`ServicoConsulta.cs:84`).
- **"Gerador de resposta (Azure OpenAI)"** é o `GeradorRespostaAzureOpenAI`. O modelo é o
  deployment configurado em `AzureOpenAI:DeploymentChat`, e o de embeddings vem de
  `AzureOpenAI:DeploymentEmbedding`. Nenhum dos dois está fixo no código: os nomes aparecem só
  como exemplos nas mensagens de erro (`gpt-5.6-luna`, `text-embedding-3-large`).

### Versão simplificada (`figura5-sequencia-rrf-simplificada`)

Mostra o mesmo fluxo com 8 participantes em vez de 14 e cabe em 976×689 px (a original tem
1892×1306 px). O que saiu, e onde continua documentado:

- **`:ConsultasController` e `:ServicoConsulta` viraram um participante só, `Codex20.Api`.** A
  nota "Antes de consultar" (limites, chave, tamanho) saiu; as verificações estão descritas na
  tabela da Figura 5 acima. A conversão para `RespostaConsulta` continua como mensagem para a
  própria Api.
- **Cada perna esconde o próprio acesso ao banco.** `:GeradorEmbeddingAzureOpenAI`,
  `:ArmazenamentoVetorialSqlite` e `:IndiceTextualFts5` saíram. A resposta de cada perna diz o
  que foi usado ("embedding + vec0", "FTS5"). As pernas continuam em sequência, como no código.
- **`MontadorPrompt` e `LeitorCitacoes` viraram passos do `:PipelineRag`** ("monta o prompt",
  "lê as citações"). São classes estáticas chamadas pelo pipeline, então a mensagem para o
  próprio pipeline não muda o significado.
- **O `alt` sem trechos saiu.** Como explicado acima, pelo webApp ele só acontece com o banco
  vazio.
- **As mensagens foram resumidas.** `ResponderAsync(rrf, pergunta)` resume
  `ResponderAsync(rrf, pedido)` com `pedido.Pergunta`; `BuscarAsync(8 trechos)` e
  `BuscarAsync(50)` resumem os `PedidoBusca` com `Quantidade = 8` e a quantidade de cada perna
  (max(8, 50) = 50).
