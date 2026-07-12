# Jornadas de trabalho (Work Schedules)

Define os horários que cada usuário deve cumprir — usado para calcular atrasos,
horas extras e para decidir se uma atividade capturada aconteceu "dentro" ou
"fora" do expediente (o que outras features, como App Focus, podem usar para
qualificar o que está sendo monitorado).

Arquivos: `Controllers/WorkScheduleController.cs`, `Services/WorkScheduleService.cs`,
`Repositories/WorkScheduleRepository.cs`, `Models/WorkSchedule.cs`,
`Models/WorkScheduleRule.cs`, `Models/UserWorkSchedule.cs`, `Models/WorkDay.cs`,
`Models/WorkScheduleType.cs`, `Utils/WorkScheduleValidator..cs` (nome do arquivo
tem um `.` duplicado propositalmente ou por erro de digitação — vale corrigir
em algum momento, mas não afeta a compilação).

## Modelo de dados

- **`WorkSchedule`**: o "cronograma" em si — `Name`, `Description`,
  `ScheduleCode`, `Type` (`WorkScheduleType`: Fixed, FiveTwo, SixOne,
  TwelveThirtySix, Supervision, Custom), `MonitorOutsideSchedule`,
  `MonitorIdleTime`, `IsActive`. Pertence a uma `OrganizationId` (multi-tenant).
- **`WorkScheduleRule`**: uma regra por dia da semana (`WorkDay`: Monday=1 ...
  Sunday=7) dentro de um `WorkSchedule` — `StartTime`/`EndTime` (`TimeSpan`),
  `BreakDuration`, `ToleranceMinutes`, `AllowOvertime`, `CrossesMidnight`
  (turno que passa da meia-noite), e `BlocksJson` — uma lista de
  `WorkScheduleBlockDTO` (`StartMinutes`/`EndMinutes`) serializada como JSON,
  provavelmente para representar múltiplos blocos de trabalho no mesmo dia
  (ex.: manhã + tarde com intervalo no meio) além do intervalo único de
  `BreakDuration`.
- **`UserWorkSchedule`**: associação usuário↔cronograma com vigência
  (`StartDate`/`EndDate`, `IsActive`) — permite manter histórico de qual
  jornada um usuário teve em cada período, não apenas a atual.

**Nota de nomenclatura**: `WorkScheduleRuleDTO` representa horários em minutos
desde 00:00 (`StartTimeMinutes`/`EndTimeMinutes`) em vez de `TimeSpan` — mapeamento
feito manualmente em `WorkScheduleService.MapRuleFromDTO`/`WorkScheduleResponseDTO.FromEntity`.

## Endpoints

Base: `api/work-schedules`. Todos exigem `[Authorize]` (JWT válido), sem
verificação adicional de permissão granular (`HasPermission`) — qualquer
usuário autenticado do tenant pode gerenciar cronogramas.

| Método | Rota | Uso |
|---|---|---|
| `POST` | `/` | Cria um cronograma (`Organization-Id` no header) |
| `GET` | `/` | Lista todos os cronogramas da organização |
| `GET` | `/{id}` | Busca um cronograma por id (não filtra por organização — ver limitação abaixo) |
| `PUT` | `/{id}` | Atualiza um cronograma (substitui todas as `Rules`) |
| `DELETE` | `/{id}` | Remove um cronograma |
| `POST` | `/assign` | Associa um cronograma a um usuário (`AssignUserScheduleDTO`: `UserId`, `WorkScheduleId`) |
| `POST` | `/{id}/clone` | Duplica um cronograma existente com um novo nome |
| `GET` | `/{id}/users` | Lista os usuários associados a esse cronograma |
| `GET` | `/users/{userId}/history` | Histórico de jornadas que um usuário já teve |
| `GET` | `/users/{userId}/current` | Jornada vigente do usuário hoje, incluindo a regra específica do dia da semana atual |

## Regras de negócio

- **Validação** (`WorkScheduleValidator`): nome obrigatório, ao menos uma
  regra, dia da semana entre 0-6 (nota: a validação usa `0-6`, mas o enum
  `WorkDay` real vai de `1` a `7` — inconsistência entre validador e enum que
  vale revisar), início antes do fim, intervalo não pode ser negativo nem
  maior/igual à duração total do turno, tolerância entre 0 e 120 minutos.
- **`AssignToUserAsync`** remove **todas** as associações anteriores do
  usuário antes de criar a nova (`RemoveRange` de todos `UserWorkSchedule`
  daquele `UserId`) — ou seja, um usuário só pode ter uma jornada "ativa"
  por vez neste fluxo; não preserva `EndDate` da associação anterior para
  formar histórico automaticamente (o histórico via `GetUserHistoryAsync`
  só existiria se essas linhas antigas fossem mantidas com `EndDate`
  preenchido, o que este método não faz — hoje ele apaga, não encerra).
- **`GetCurrentScheduleAsync`** calcula o dia da semana atual em UTC
  (`DateTime.UtcNow.DayOfWeek`, convertendo domingo de `0` para `7` para
  bater com o enum `WorkDay`) e retorna apenas a regra daquele dia
  específico — útil para o dashboard mostrar "hoje você trabalha das X às Y".
- **Clonagem** (`CloneAsync`) copia todas as `Rules` preservando os campos,
  mas gera novos `Id`s tanto para o cronograma quanto para as regras (evita
  colisão de chave primária).

## Limitações conhecidas

- `GetById`/`Update`/`Delete` não recebem/validam `OrganizationId` — um
  usuário autenticado poderia, em tese, manipular o cronograma de outra
  organização se souber o `Guid`. As outras rotas (`GetAll`, `assign`,
  histórico) já isolam por organização; vale alinhar `GetById`/`Update`/`Delete`
  ao mesmo padrão.
- Não há verificação de que o cronograma pertence à mesma organização do
  usuário autenticado no fluxo de `assign` — o endpoint aceita qualquer
  `WorkScheduleId`/`UserId` informado no corpo, sem cruzar com o token.
