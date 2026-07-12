# Autorização e permissões — WiseMonitor

Este documento descreve o mecanismo de controle de acesso usado em praticamente
todos os módulos da API: quem pode chamar qual endpoint, e como isso é
decidido. É referenciado pelos docs de `superadmin-*.md`, mas vale para o
sistema inteiro (Teams, Departments, Screenshots, etc. também usam
`[HasPermission(...)]`).

## 1. Duas camadas de controle, não uma

O sistema combina **dois mecanismos distintos** de autorização, que se
sobrepõem em várias rotas:

1. **`[Authorize]` (JWT válido)** — do ASP.NET Core padrão: exige apenas um
   token válido (qualquer usuário autenticado passa).
2. **`[SuperAdminOnly]`** (`Authorization/SuperAdminAuthorizationFilter.cs`) —
   um `IAuthorizationFilter` customizado, aplicado a nível de classe em
   `SuperAdminBaseController` (da qual todos os 8 controllers
   `SuperAdmin*Controller` herdam). Verifica se `User.IsInRole("SuperAdmin")`
   OU se a claim `isSuperAdmin` é `"true"`. Se não for, retorna `403 Forbid`
   direto no pipeline de filtros — nem chega a entrar no controller.
3. **`[HasPermission(Permissions.X)]`** (`Authorization/HasPermissionAttribute.cs`)
   — uma sub-classe de `AuthorizeAttribute` que declara uma *policy* dinâmica
   `"Permission:{permission}"`. Essa policy é resolvida por
   `PermissionAuthorizationHandler`, que lê a claim de role do usuário
   (`ClaimTypes.Role` ou `"role"`) e consulta `RolePermissionMatrix.HasPermission(role, permission)`.

Ou seja: um endpoint dentro de `SuperAdmin*Controller` com
`[HasPermission(Permissions.TenantsView)]` passa por **dois** checks
independentes — primeiro o filtro de classe (`SuperAdminOnly`), depois a
policy do atributo do método. Na prática isso é redundante hoje: como só o
papel `SuperAdmin` tem as permissões `Tenants*`/`MetricsGlobal` na matriz (ver
seção 3), o filtro de classe já barra qualquer não-SuperAdmin antes do
`HasPermission` sequer ser avaliado. A dupla checagem só passaria a fazer
diferença se um papel não-SuperAdmin ganhasse alguma dessas permissões no
futuro.

## 2. `RolePermissionMatrix` — hardcoded, não editável em runtime

`Authorization/RolePermissionMatrix.cs` é um `Dictionary<string, HashSet<string>>`
**estático, compilado no binário** — não uma tabela no banco. Isso significa:

- Não existe UI/endpoint para editar "o que o papel Manager pode fazer" sem
  recompilar e reimplantar a API. (`SuperAdminUsersController`/`Permissions.RolesManage`
  sugerem que a intenção original era permitir gestão de papéis, mas hoje é
  só leitura fixa.)
- Papéis conhecidos: `SuperAdmin`, `TenantAdmin`, `Director`, `Manager`,
  `Supervisor`, `Employee`, `Auditor`, `HR`, `Financial`, `ProjectManager`,
  `Client` (`Models/Enums/UserRoles.cs`). `SuperAdmin` tem todas as
  permissões; os demais têm subconjuntos específicos de domínio (ex.:
  `Employee` só vê seus próprios dados — a filtragem por usuário é feita na
  camada de serviço, a permissão em si não distingue "próprio" de "todos").
- `RolePermissionMatrix.HasPermission`/`GetPermissions` normalizam o nome do
  papel via `UserRoles.Normalize(role)` antes de consultar o dicionário —
  então variações de case/sinônimos do claim de role não quebram o lookup.

## 3. Permissões exclusivas de SuperAdmin

`Permissions.TenantsView/Create/Edit/Delete/Suspend` e
`Permissions.MetricsGlobal` só existem na entrada `[UserRoles.SuperAdmin]` da
matriz — nenhum outro papel as possui. Isso reforça o ponto da seção 1: hoje
essas permissões são, na prática, um sinônimo redundante de "é SuperAdmin".

## 4. Inconsistência observada: nem todo controller SuperAdmin usa `[HasPermission]`

`SuperAdminTenantsController`, `SuperAdminUsersController`,
`SuperAdminMetricsController` e `SuperAdminAuditController` decoram **todas**
as ações com `[HasPermission(Permissions.X)]`. Já
`SuperAdminAlertsController`, `SuperAdminIntegrationsController` e
`SuperAdminSettingsController` **não têm nenhum** `[HasPermission]` nas suas
ações — dependem só do `[SuperAdminOnly]` herdado da base. Funcionalmente o
efeito é quase o mesmo hoje (só SuperAdmin passa pelo filtro de classe de
qualquer forma), mas é uma inconsistência de padrão: se um dia a matriz
ganhar um papel intermediário tipo "PlatformSupport" com acesso a alertas mas
não a tenants, essas ações não teriam como restringir por permissão — só por
"é ou não é SuperAdmin".

## 5. Onde isso aparece nos outros módulos

Fora do SuperAdmin, `[HasPermission(...)]` também protege ações em módulos de
tenant normais (ex.: Teams, Departments, Screenshots, Reports) — o padrão é o
mesmo `AuthorizationHandler` e a mesma `RolePermissionMatrix`, só que sem o
filtro adicional `[SuperAdminOnly]`. Ver os docs de cada módulo para o mapa
exato de qual permissão cada endpoint exige.
