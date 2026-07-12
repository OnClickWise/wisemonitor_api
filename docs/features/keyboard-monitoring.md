# Monitoramento de teclado (Keyboard)

Mede atividade de digitação por sessão — quantidade de teclas, letras,
palavras, números e símbolos — e deriva um "score de produtividade" a partir
disso, **sem armazenar o conteúdo literal digitado** (só contagens agregadas).
É um dos dois sinais (junto com App Focus) correlacionados com os segmentos de
vídeo no histórico ao vivo (ver `docs/video-streaming-flow.md`, seção 4.4).

Arquivos: `Controllers/KeyboardController.cs`, `Services/KeyboardService.cs`,
`Repositories/KeyboardRepository.cs`, `Helpers/KeyboardProductivityHelper.cs`,
`Models/KeyboardSession.cs`, `Models/KeyboardWord.cs`,
`Models/KeyboardWordCategory.cs`, `Models/KeyboardClassification.cs`.

## Modelo de dados

- **`KeyboardSession`**: uma janela de tempo de digitação —
  `StartAt`/`EndAt`, `Application` (em qual app se digitou), contagens
  (`TotalKeystrokes`, `LettersCount`, `WordsCount`, `NumbersCount`,
  `SymbolsCount`), `ProductivityScore` (int calculado), `Classification`
  (`KeyboardClassification`: Produtivo/Neutro/Improdutivo).
- **`KeyboardWord`**: entidade relacionada (`KeyboardSessionId`, `Word`,
  `Count`, `Category`: `KeyboardWordCategory` Produtiva/Neutra/Improdutiva) —
  existe no modelo e na tabela, mas **`KeyboardService.ProcessKeyboardEventAsync`
  nunca cria nenhum `KeyboardWord`**, mesmo o DTO de entrada
  (`KeyboardEventCreateDTO.Words`) trazendo uma lista de palavras do desktop.
  Ou seja, hoje a palavra-por-palavra chega da rede mas é descartada — só as
  contagens agregadas (`Metrics`) são persistidas. Isso é relevante porque
  `docs/video-streaming-flow.md` descreve o histórico como mostrando "o que
  foi digitado" a partir de `KeyboardSession` — na prática, isso hoje é só a
  contagem de palavras/letras, não as palavras em si.

## Endpoints

Base: `api/keyboard`. **Nenhuma rota tem `[Authorize]`** neste controller
(diferente de quase todos os outros módulos) — qualquer requisição sem token
válido ainda cai em `User.GetUserId()`/`GetOrganizationId()` (extension
methods sobre `ClaimsPrincipal`), que provavelmente devolvem `Guid.Empty`
quando não há usuário autenticado, silenciosamente associando a sessão ao
"usuário vazio" em vez de rejeitar a requisição. Vale alinhar este controller
ao padrão `[Authorize]` dos demais.

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
  do App Focus.
- **`GetSummaryAsync`** agrupa todas as sessões do período num único grupo
  (`GroupBy(_ => 1)`) e faz média do `ProductivityScore` — mas a
  `Classification` do resumo é **sempre fixada como `Produtivo`**
  (`Classification = KeyboardClassification.Produtivo` hardcoded no
  `KeyboardRepository.GetSummaryAsync`), independente do score médio
  calculado. Isso é um bug real: o resumo nunca reporta "Neutro" ou
  "Improdutivo" mesmo que a média indique isso.
- `Id` da sessão (`dto.SessionId`) é definido pelo **cliente** (o desktop
  gera o `Guid` da sessão localmente e o envia), não pelo servidor — permite
  ao desktop reconhecer a mesma sessão em caso de reenvio/retry (idempotência
  parcial), mas também significa que o servidor confia no `Guid` recebido
  sem verificar unicidade antes do insert (colisão causaria exceção do EF
  Core por chave duplicada).
- `GetHistoryAsync`/`GetSummaryAsync` filtram por `StartAt >= start && EndAt
  <= end` — uma sessão que começa antes de `start` mas termina depois não
  aparece (não é overlap, é contenção total no intervalo).

## Quem envia isso no desktop

`KeyboardAgentService` + `KeyboardBufferService` (via
`Utils/KeyboardHookHelper`, um hook global `WH_KEYBOARD_LL`) capturam teclas
digitadas, classificam/agregam localmente (contagens, não o texto bruto) e
`KeyboardApiService` envia para `POST /api/keyboard/events` periodicamente,
com retry offline via `OfflineEventQueueService` em caso de falha de rede.
