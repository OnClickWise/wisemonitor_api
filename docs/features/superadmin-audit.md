# SuperAdmin — Auditoria da Plataforma (cross-tenant)

Visão de auditoria **sem filtro de organização** — todo SuperAdmin autenticado
com a permissão certa vê logs de todos os tenants. É a contraparte
"plataforma inteira" do módulo `AuditLogController` normal (por-tenant),
documentado em `docs/features/audit-logs.md` — ambos compartilham a mesma
tabela `AuditLog` e o mesmo `IAuditService`; a única diferença é o filtro de
`organizationId` (aqui, sempre `null` = sem filtro).

## 1. Endpoints (`SuperAdminAuditController`, base `api/super-admin/audit-logs`)

| Método | Rota | Permissão | Uso |
|---|---|---|---|
| `GET` | `/` | `AuditLogsView` | Lista paginada/filtrada de logs de toda a plataforma |
| `GET` | `/export` | `AuditLogsView` | Exporta até 10.000 linhas em CSV |

## 2. Exportação CSV

`Export` força `filter.PageSize = 10_000` e `filter.Page = 1` antes de
consultar — ou seja, ignora qualquer paginação que o cliente tenha passado
na query string e sempre tenta trazer até 10 mil linhas numa única resposta.
Monta o CSV manualmente (`StringBuilder`, sem biblioteca de CSV), com uma
função `Escape` local que só entra em ação se o valor contiver vírgula,
aspas ou quebra de linha (regra padrão de CSV: envolve em aspas duplas e
duplica aspas internas). Cabeçalho fixo:
`Id,OrganizationId,UserEmail,UserRole,Action,EntityType,EntityId,IpAddress,Success,Details,CreatedAt`.
Nome do arquivo inclui a data UTC do momento da exportação
(`audit-log-{yyyy-MM-dd}.csv`).

**Limitação implícita**: se a plataforma tiver mais de 10.000 entradas de
auditoria no filtro solicitado, a exportação simplesmente trunca nas
primeiras 10 mil (ordenadas conforme o filtro) — não há paginação da
exportação em si nem aviso de truncamento na resposta.

## 3. Relação com `AuditLogController` (por-tenant)

Ambos os controllers chamam o mesmo `IAuditService.GetLogsAsync(filter, organizationId)` —
a diferença inteira está em qual valor de `organizationId` é passado:
`SuperAdminAuditController` sempre passa `null` (sem filtro, vê tudo);
`AuditLogController` (tenant normal) passa o `organizationId` do usuário
logado, restringindo à própria organização. Ver `docs/features/audit-logs.md`
para o funcionamento completo de `IAuditService`, o middleware que gera
entradas automaticamente, e o formato de `AuditLogFilterDTO`/`AuditLogDTO`.
