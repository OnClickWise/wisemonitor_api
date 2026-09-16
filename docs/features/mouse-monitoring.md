# Monitoramento de mouse (Mouse)

Mede atividade de mouse por sessão — cliques (esquerdo/direito/meio) e
quantidade de scroll — no mesmo espírito do módulo de teclado
([`keyboard-monitoring.md`](./keyboard-monitoring.md)): só contagens
agregadas, nenhuma coordenada de tela ou conteúdo é armazenado.

Arquivos: `Controllers/MouseController.cs`, `Services/MouseService.cs`,
`Repositories/MouseRepository.cs`, `Models/MouseSession.cs`.

## Modelo de dados

- **`MouseSession`**: uma janela de tempo de uso do mouse — `StartAt`/`EndAt`,
  `Application` (em qual app o mouse foi usado), contagens (`LeftClicks`,
  `RightClicks`, `MiddleClicks`, `ScrollCount`). Sem sub-entidade equivalente
  a `KeyboardWord` — cliques não carregam conteúdo, então não há nada
  detalhado para persistir por evento, só os totais agregados por sessão.
- Tabela `MouseSessions`, isolada por tenant via o mesmo `HasQueryFilter`
  baseado em `ITenantContext` usado pelas demais entidades
  (`Data/AppDbContext.cs`), e indexada por `(OrganizationId, UserId,
  StartAt)` + `UserId`, espelhando `KeyboardSessions`.

## Endpoints

Base: `api/mouse`. Controller decorado com `[Authorize]` (diferente do
`KeyboardController` original do TimeZoon, que não tinha — aqui os dois já
nascem/foram corrigidos para exigir autenticação). As rotas de leitura
(`{id}`, `history`, `summary`) exigem também `Permissions.ActivityView`; a
criação (`POST events`) é o próprio agent de desktop enviando os dados do
usuário logado, então só precisa de `[Authorize]`.

| Método | Rota | Uso |
|---|---|---|
| `POST` | `/events` | Cria uma sessão de mouse (`MouseEventCreateDTO`) |
| `GET` | `/{id}` | Busca uma sessão por id (escopada ao usuário) |
| `GET` | `/history?start=&end=` | Lista sessões do usuário num período |
| `GET` | `/summary?start=&end=` | Resumo agregado (soma de cliques/scroll) |
| `PUT` | `/{id}` | Atualiza contagens de uma sessão existente |
| `DELETE` | `/{id}` | Remove uma sessão |

## Regras de negócio

- Sem cálculo de produtividade/classificação — ao contrário do teclado, o
  mouse não tem um `MouseClassification`; `GetSummaryAsync` só soma os
  quatro contadores.
- `Id` da sessão (`dto.SessionId`) é definido pelo **cliente**, mesmo padrão
  do teclado (idempotência parcial em caso de retry; sem verificação de
  unicidade antes do insert).
- `GetHistoryAsync`/`GetSummaryAsync` filtram por `StartAt >= start && EndAt
  <= end` (contenção total no intervalo, não overlap) — mesmo comportamento
  do módulo de teclado.

## Quem envia isso no desktop

`MouseAgentService` + `MouseBufferService` (via `Utils/MouseHookHelper`, um
hook global `WH_MOUSE_LL`) capturam cliques/scroll, agregam localmente e
`MouseApiService` envia para `POST /api/mouse/events` periodicamente, com
retry offline via `OfflineEventQueueService` (mesmo mecanismo do teclado).
