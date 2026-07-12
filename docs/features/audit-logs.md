# Logs de auditoria (Audit Logs)

Registro de eventos sensíveis do sistema — hoje, na prática, apenas acessos
negados (`403`) são gravados automaticamente; qualquer outro tipo de entrada
(login, exclusão, alteração de permissão) depende de algum código chamar
`IAuditService.LogAsync(...)` explicitamente.

Arquivos: `Controllers/AuditLogController.cs`, `Services/AuditService.cs`,
`Middlewares/AuditMiddleware.cs`, `Models/AuditLog.cs`,
`DTOs/AuditLog/AuditLogDTO.cs`.

## Modelo de dados

`AuditLog`: `OrganizationId` (nullable — `null` significa ação do SuperAdmin
fora de qualquer tenant), `UserId`, `UserEmail`, `UserRole`, `Action` (string
livre, convenção documentada em comentário no modelo: `login`, `logout`,
`view`, `export`, `create`, `update`, `delete`, `permission_change`,
`delegation`, `access_denied`), `EntityType` (`User`, `Team`, `Department`,
`Screenshot`, etc.), `EntityId`, `OldValue`/`NewValue` (JSON serializado,
para diffs de auditoria), `IpAddress`, `UserAgent`, `Success`, `Details`,
`CreatedAt`.

## Endpoints

Base: `api/audit-logs`. Exige `[Authorize]` + permissão granular
`[HasPermission(Permissions.AuditLogsView)]` (ver
`docs/features/authorization-permissions.md` para como esse sistema funciona).

| Método | Rota | Uso |
|---|---|---|
| `GET` | `/?action=&entityType=&userId=&from=&to=&page=&pageSize=` | Lista logs paginados, com filtros opcionais |

## Regras de negócio

- **Escopo por papel**: se o `role` do usuário autenticado (normalizado via
  `UserRoles.Normalize`) for `SuperAdmin`, a consulta não filtra por
  organização — vê logs de **todos os tenants**. Qualquer outro papel só vê
  logs da própria organização (`User.GetOrganizationId()`), mesmo que possua
  a permissão `AuditLogsView` — o filtro de tenant é aplicado incondicionalmente
  para não-SuperAdmins.
- **Registro automático de acesso negado**: `AuditMiddleware` roda para
  **toda** requisição da pipeline e, depois que a resposta é gerada, verifica
  se o status foi `403`. Se sim, grava um `AuditLog` com
  `Action = "access_denied"`, `EntityType = "Endpoint"`, `EntityId` = o path
  da requisição, IP e User-Agent — isso cobre automaticamente qualquer
  controller que recuse acesso (seja por `[Authorize]`, por
  `[HasPermission]`, ou por `Forbid()` manual), sem cada controller precisar
  chamar o serviço de auditoria manualmente para esse caso específico.
- **Falha de auditoria não derruba a requisição principal**: `AuditService.
  LogAsync` envolve o `SaveChangesAsync` em try/catch e apenas loga o erro —
  um problema no banco ao gravar auditoria nunca deve quebrar o fluxo de
  negócio que está sendo auditado.
- **Login/logout não estão automaticamente cobertos** pelo middleware (que só
  reage a `403`) — dependem de chamadas explícitas a `LogAsync` dentro de
  `AuthController`/`AuthService` para esses `Action`s aparecerem no log; não
  foi confirmado neste levantamento se esse código de chamada explícita
  existe hoje no `AuthController` — vale checar se logins bem-sucedidos estão
  de fato sendo auditados, já que o middleware não cobre esse caso.
- Só existe leitura (`GET`) — não há endpoint para exportar ou apagar logs de
  auditoria (esperado para um sistema de auditoria: os registros devem ser
  imutáveis e não removíveis pela própria API).

## Relação com outros módulos

Este é um dos poucos módulos "transversais" do sistema: qualquer controller
pode (e idealmente deveria) chamar `IAuditService.LogAsync` para registrar
ações sensíveis específicas (ex.: alteração de permissão de um usuário,
exclusão de um dispositivo). Hoje a cobertura automática via middleware cobre
só o caso de acesso negado — o restante depende de instrumentação manual, que
não foi mapeada de forma exaustiva neste documento (exigiria varrer todos os
controllers em busca de chamadas a `LogAsync`).
