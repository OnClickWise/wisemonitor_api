# Monitoramento de teclado (Keyboard)

Mede atividade de digitação por sessão — quantidade de teclas, letras,
palavras, números e símbolos — e deriva um "score de produtividade" a partir
disso, **sem armazenar o conteúdo literal digitado** (só contagens agregadas
e, desde a captura de qualidade, também a lista de palavras+contagem que o
desktop já agrega localmente — ver abaixo). É um dos dois sinais (junto com
App Focus) correlacionados com os segmentos de vídeo no histórico ao vivo
(ver `docs/video-streaming-flow.md`, seção 4.4). Ver também
[`mouse-monitoring.md`](./mouse-monitoring.md), o módulo irmão adicionado
junto com as métricas de qualidade.

Arquivos: `Controllers/KeyboardController.cs`, `Services/KeyboardService.cs`,
`Repositories/KeyboardRepository.cs`, `Helpers/KeyboardProductivityHelper.cs`,
`Models/KeyboardSession.cs`, `Models/KeyboardWord.cs`,
`Models/KeyboardWordCategory.cs`, `Models/KeyboardClassification.cs`.

## Modelo de dados

- **`KeyboardSession`**: uma janela de tempo de digitação —
  `StartAt`/`EndAt`, `Application` (em qual app se digitou), contagens
  (`TotalKeystrokes`, `LettersCount`, `WordsCount`, `NumbersCount`,
  `SymbolsCount`), `ProductivityScore` (int calculado), `Classification`
  (`KeyboardClassification`: Produtivo/Neutro/Improdutivo), e as métricas de
  qualidade: `BackspaceCount`, `WordsPerMinute`, `CorrectionRate`
  (`BackspaceCount / TotalKeystrokes`) — calculadas em
  `KeyboardProductivityHelper.CalculateQuality`.
- **`KeyboardWord`**: entidade relacionada (`KeyboardSessionId`, `Word`,
  `Count`, `Category`: `KeyboardWordCategory` Produtiva/Neutra/Improdutiva).
  `KeyboardService.ProcessKeyboardEventAsync` agora persiste cada item de
  `KeyboardEventCreateDTO.Words` (uma lista de `{Word, Count}`, não mais
  strings soltas) como um `KeyboardWord`. **Limitação conhecida**: não existe
  nenhum classificador de produtividade por palavra (nem no backend, nem no
  desktop), então `Category` é fixado em `Neutra` para toda palavra
  persistida — deliberadamente não `Produtiva` (ordinal 0 do enum), para não
  inflar estatísticas de produtividade sem uma classificação real por trás.
  A entidade tinha um bug de serialização (referência circular
  `Session→Words→Session→...`) — corrigido com `[JsonIgnore]` na propriedade
  de navegação reversa `KeyboardWord.KeyboardSession`.

## Endpoints

Base: `api/keyboard`. O controller agora tem `[Authorize]` a nível de classe
(gap corrigido — antes nenhuma rota exigia autenticação, e uma requisição sem
token caía silenciosamente em `Guid.Empty` via `User.GetUserId()`/
`GetOrganizationId()`). As rotas de leitura (`{id}`, `history`, `summary`)
também exigem `Permissions.ActivityView`; a criação (`POST events`) é o
próprio agent de desktop enviando os dados do usuário logado, então só
precisa de `[Authorize]`.

| Método | Rota | Uso |
|---|---|---|
| `POST` | `/events` | Cria uma sessão de teclado (`KeyboardEventCreateDTO`) |
| `GET` | `/{id}` | Busca uma sessão por id (escopada ao usuário) |
| `GET` | `/history?start=&end=` | Lista sessões do usuário num período |
| `GET` | `/summary?start=&end=` | Resumo agregado (total de teclas/palavras, score médio) |
| `PUT` | `/{id}` | Atualiza contagens de uma sessão existente |
| `DELETE` | `/{id}` | Remove uma sessão |

## Regras de negócio

- **Cálculo de produtividade** (`KeyboardProductivityHelper.Calculate`):
  `score = (palavras × 2) + (letras × 0.1) − (símbolos × 0.5)`; classificação
  por faixa: `>= 70` Produtivo, `>= 40` Neutro, abaixo disso Improdutivo. É
  uma heurística simples e arbitrária (não considera contexto de app, por
  exemplo) — mesmo espírito de simplicidade do `ActivityClassificationService`
  do App Focus. `Calculate`/`CalculateQuality` toleram `dto.Metrics == null`
  (fazem fallback para um `KeyboardMetricsDTO` vazio em vez de lançar NRE).
- **Cálculo de qualidade** (`KeyboardProductivityHelper.CalculateQuality`):
  `WordsPerMinute = palavras / duração-em-minutos`, `CorrectionRate =
  BackspaceCount / TotalKeystrokes` (ambos `0` se o denominador for `0`).
  Assim como o score de produtividade, é uma heurística simples — sem piso/
  teto, então um `StartAt`/`EndAt` com relógio dessincronizado pode gerar
  valores absurdos; não há proteção contra isso hoje.
- **`GetSummaryAsync`** agrupa todas as sessões do período num único grupo
  (`GroupBy(_ => 1)`), soma os totais em SQL, e então calcula a
  `Classification` do resumo a partir da média do `ProductivityScore`
  (mesmas faixas do `Calculate` acima) — corrigido o bug em que a
  classificação do resumo vinha sempre fixada como `Produtivo`.
- `Id` da sessão (`dto.SessionId`) é definido pelo **cliente** (o desktop
  gera o `Guid` da sessão localmente e o envia), não pelo servidor — permite
  ao desktop reconhecer a mesma sessão em caso de reenvio/retry (idempotência
  parcial), mas também significa que o servidor confia no `Guid` recebido
  sem verificar unicidade antes do insert (colisão causaria exceção do EF
  Core por chave duplicada).
- `GetHistoryAsync`/`GetSummaryAsync` filtram por `StartAt >= start && EndAt
  <= end` — uma sessão que começa antes de `start` mas termina depois não
  aparece (não é overlap, é contenção total no intervalo).
- `GetByIdAsync`/`GetHistoryAsync` agora fazem `.Include(x => x.Words)`, então
  a lista de palavras persistida vem junto na resposta (antes não vinha,
  mesmo existindo na tabela).

## Quem envia isso no desktop

`KeyboardAgentService` + `KeyboardBufferService` (via
`Utils/KeyboardHookHelper`, um hook global `WH_KEYBOARD_LL`) capturam teclas
digitadas, classificam/agregam localmente (contagens, não o texto bruto) e
`KeyboardApiService` envia para `POST /api/keyboard/events` periodicamente,
com retry offline via `OfflineEventQueueService` em caso de falha de rede.
