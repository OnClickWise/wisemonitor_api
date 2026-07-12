# Monitoramento de aplicações (App Focus)

Registra qual aplicativo/janela esteve em foco no dispositivo monitorado, por
quanto tempo, e (quando a janela é um navegador) qual URL/favicon — a base
para relatórios de produtividade ("quanto tempo no Chrome vs. no Excel") e
para correlacionar com os segmentos de vídeo no histórico (ver
`docs/video-streaming-flow.md`, seção 4.4).

Arquivos: `Controllers/AppFocusController.cs`, `Services/AppFocusService.cs`,
`Services/ActivityClassificationService.cs`, `Repositories/AppFocusRepository.cs`,
`Models/AppFocusEvent.cs`, `Models/ActivityCategory.cs`,
`Helpers/AppFocusDurationHelper.cs`.

## Modelo de dados

`AppFocusEvent`: `OrganizationId`, `UserId`, `DeviceId` (aqui é `Guid`, ao
contrário de `Screenshot.DeviceId`/`VideoSegment.DeviceId` que são `string` —
ver nota de inconsistência em `docs/features/devices.md`), `ApplicationName`,
`ProcessName`, `WindowTitle`, `Url`/`FaviconUrl` (opcionais, preenchidos só
quando o app em foco é um navegador), `StartTime`/`EndTime` (`EndTime` é
nullable — um evento "em andamento" pode não ter fim ainda), `DurationSeconds`
(calculado no backend, não confiado ao cliente), `Category` (`ActivityCategory`:
Productive, Neutral, Unproductive).

## Endpoints

Base: `api/monitoring/app-focus`. Todos exigem `[Authorize]`.

| Método | Rota | Uso |
|---|---|---|
| `POST` | `/` | Agente desktop registra um evento de foco (`AppFocusEventCreateDTO`) |
| `GET` | `/?startDate=&endDate=` | Lista eventos da organização num período |
| `GET` | `/{id}` | Busca um evento específico (escopado à organização) |
| `PUT` | `/{id}` | Atualiza um evento existente |
| `DELETE` | `/{id}` | Remove um evento |
| `GET` | `/user/{userId}?start=&end=` | Histórico de foco de um usuário num intervalo (`end` opcional — se omitido, vira igual a `start`, ou seja, só aquele dia) |
| `GET` | `/metrics?date=` | Métricas agregadas por categoria (soma de segundos) para o usuário autenticado, num dia |

## Regras de negócio

- **Classificação automática** (`ActivityClassificationService.ClassifyAsync`):
  hoje é uma lista fixa e curta de palavras-chave no nome do app — Chrome/
  Edge/Firefox → `Productive`; Spotify/YouTube → `Unproductive`; qualquer
  outra coisa → `Neutral`. Isso é claramente um placeholder simplista (um
  navegador é "produtivo" mesmo se a aba aberta for YouTube, por exemplo) —
  seria natural evoluir para usar a `Url`/`WindowTitle` também, não só o
  nome do executável.
- **Duração calculada no servidor** (`AppFocusDurationHelper.CalculateDuration`):
  `(EndTime ?? DateTime.UtcNow) - StartTime`, nunca negativa. Eventos com
  duração calculada `<= 0` são **descartados silenciosamente** no `Register`
  (log de warning, mas retorna `200 OK` do mesmo jeito) — o desktop não tem
  como saber que o evento foi ignorado.
- **Auto-registro de `Device`**: se o `DeviceId` do evento ainda não existe,
  `RegisterEventAsync` cria um `Device` novo na hora (`Hostname = "Desktop
  Agent"` como placeholder) em vez de rejeitar o evento — ver
  `docs/features/devices.md`. Se já existe, atualiza `IsOnline`/`LastSeen`.
- **`GetByUserDateRangeAsync`** (usado por `GetHistoryAsync`/`GetMetricsAsync`)
  normaliza o `DateTimeKind` para UTC se vier como `Unspecified`, e expande a
  busca para o dia inteiro (`start.Date` até `end.Date.AddDays(1).AddTicks(-1)`)
  — ou seja, `metrics?date=2026-07-11T15:00:00` retorna dados do dia
  `2026-07-11` inteiro, não só a partir das 15h.
- `AppFocusRepository` tem métodos da interface (`GetByUserAndDateAsync`,
  `GetByIdAsync(id)` sem organização, `GetByOrganizationAsync`) que lançam
  `NotImplementedException` — código morto/incompleto que ainda não foi
  limpo da interface `IAppFocusRepository`.

## Quem envia isso no desktop

`AppFocusAgentService` (via `Utils/ActiveWindowHelper`/`Utils/BrowserInfoHelper`)
detecta a janela ativa (Win32 `GetForegroundWindow`) e extrai URL/favicon
quando é um navegador conhecido, enviando eventos conforme o foco muda.
