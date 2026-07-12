# SuperAdmin — Regras de Alerta e Histórico

CRUD de regras de alerta da plataforma (ex.: "avisar quando um novo tenant se
cadastra", "avisar se a taxa de erro da API subir") e consulta/resolução do
histórico de alertas disparados.

**Ponto mais importante deste módulo**: isto é **puro CRUD de configuração**
— não existe, em nenhum lugar do código encontrado nesta revisão, um motor
que efetivamente **avalie** as `AlertRule`s contra eventos reais do sistema e
crie linhas em `AlertHistory` automaticamente. A única escrita em
`AlertHistories` encontrada no código é dentro do próprio
`SuperAdminAlertService` (`GetHistoryAsync`/`ResolveAsync`, que só leem/
atualizam registros já existentes) — ou seja, hoje um alerta só aparece no
histórico se algo (não localizado nesta revisão — possivelmente um processo
externo, ou funcionalidade planejada e não implementada ainda) inserir
manualmente uma linha em `AlertHistories`. Trate as regras como
"configuração pronta para um motor de avaliação que ainda não existe".

## 1. Endpoints (`SuperAdminAlertsController`, base `api/super-admin/alerts`)

Nenhuma ação tem `[HasPermission(...)]` — diferente de
Tenants/Users/Metrics/Audit, este controller depende só do
`[SuperAdminOnly]` herdado da base (ver
`docs/features/authorization-permissions.md` seção 4 sobre essa
inconsistência de padrão).

| Método | Rota | Uso |
|---|---|---|
| `GET` | `/rules` | Lista todas as regras configuradas |
| `POST` | `/rules` | Cria uma nova regra |
| `PATCH` | `/rules/{id}` | Atualiza campos de uma regra |
| `DELETE` | `/rules/{id}` | Remove uma regra |
| `GET` | `/history` | Histórico paginado de alertas disparados |
| `PATCH` | `/{id}/resolve` | Marca um alerta do histórico como resolvido |

## 2. `AlertRule` — gatilhos previstos

O campo `Trigger` (`Models/AlertRule.cs`) é uma string livre (`MaxLength(50)`,
sem `enum` no banco), mas o comentário no modelo documenta os valores
esperados: `TenantSignup`, `TenantSuspended`, `PaymentFailed`,
`ErrorRateHigh`, `ResponseTimeHigh`, `StorageLimitNear`, `SuspiciousLogin`,
`BulkUserDelete`, `ImpersonationUsed`, `AgentVersionMismatch`. Isso é uma
lista de "intenção de produto" — nenhum desses eventos tem, hoje, um
disparador automático correspondente encontrado no código (ex.: nada em
`SuperAdminTenantsController.Impersonate` cria uma `AlertHistory` para
`ImpersonationUsed`, apesar do nome do trigger sugerir exatamente isso).

Demais campos: `ConditionOperator` (`gt`/`lt`/`eq`/`contains`, também string
livre) + `ConditionValue`, `Severity` (`Info`/`Warning`/`Critical`),
`NotificationChannelsJson`/`NotificationRecipientsJson` (arrays serializados
como JSON em colunas de texto — `["email","slack","webhook"]`,
`["admin@example.com"]`) e `IsActive`.

## 3. `AlertHistory` — consulta e resolução

`AlertHistoryResponseDTO` inclui o nome da regra associada
(`h.AlertRule.Name`, via `Include`). `ResolveAsync` marca
`IsResolved = true`, grava `ResolvedByUserId` (quem resolveu) e
`ResolvedAt` — simples, sem side-effects (não notifica ninguém, não reabre
automaticamente).

## 4. Relação com outros módulos

- `NotificationChannelsJson` do tipo `"webhook"`/`"slack"` provavelmente se
  conectaria, num motor de alertas real, às integrações configuradas em
  `docs/features/superadmin-integrations.md` — mas hoje não há nenhum código
  que de fato una as duas coisas (nenhuma referência cruzada entre
  `AlertRule` e `PlatformIntegration` no código).
