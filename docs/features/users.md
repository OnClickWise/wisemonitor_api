# Usuários (User)

CRUD de usuários dentro de uma organização — os "empregados monitorados" e
também os administradores/gestores que usam o sistema.

## 1. Modelo de dados (`Models/User.cs`)

Campos principais: `FirstName`/`LastName` (+ `FullName` computado),
`Email`, `AvatarUrl`, `PasswordHash`, `Role` (string livre, mas convencionada
via `UserRoles`), `IsSuperAdmin` (flag de plataforma, **separada** de `Role` —
um usuário pode ter `Role = "Employee"` e ainda assim `IsSuperAdmin = true`
teoricamente, embora na prática SuperAdmins normalmente tenham
`OrganizationId == null`), `IsActive`, `OrganizationId` (nulo só para
SuperAdmin de plataforma). Tem navegação reversa para `DepartmentMemberships`
e `TeamMemberships`.

### Papéis (`Models/Enums/UserRoles.cs`)

Onze papéis: `SuperAdmin`, `TenantAdmin`, `Director`, `Manager`,
`Supervisor`, `Employee`, `Auditor`, `HR`, `Financial`, `ProjectManager`,
`Client`. Existe uma hierarquia numérica (`GetHierarchyLevel`, menor = mais
poder: `SuperAdmin`=0 até `Client`=7) usada em checagens de permissão em
outros módulos (ex.: quem pode ser gestor de uma `Team`, ver
[`teams.md`](teams.md)).

`UserRoles.Normalize(role)` mapeia valores legados (`"admin"` → `TenantAdmin`,
`"user"` → `Employee`) para o valor canônico, e cai em `Employee` como
default se o valor não bater com nada — **isso significa que um role
digitado errado silenciosamente vira `Employee`** em vez de gerar erro; tanto
`UserController.CreateUser` quanto `UpdateUser` normalizam o `Role` do DTO
antes de persistir.

## 2. Endpoints e regras de negócio

Todos exigem `[Authorize]` a nível de controller, mais uma permissão
específica via `[HasPermission(...)]` por ação (ver
[`authorization-permissions.md`](authorization-permissions.md)):

- `POST /` (`Permissions.UsersCreate`): cria o usuário já vinculado à
  organização do token (`GetOrganizationId()` — lido diretamente do claim
  `"orgId"`, não via a extension method `User.GetOrganizationId()` usada em
  outros controllers; mesmo resultado, implementação duplicada). Hash da
  senha com `BCrypt.HashPassword` (work factor default, diferente do
  `workFactor: 12` usado em [`register-organization.md`](register-organization.md)).
  Gera um registro de auditoria (`IAuditService.LogAsync`, action `"create"`).
- `GET /` (`Permissions.UsersView`): lista todos os usuários da organização
  (sem paginação).
- `GET /{id}` (`Permissions.UsersView`): usuário específico, `404` se não
  pertencer à organização do token.
- `PUT /{id}` (`Permissions.UsersEdit`): atualização parcial — só os campos
  não-nulos do `UserUpdateDTO` são aplicados. Também audita.
- `DELETE /{id}` (`Permissions.UsersDelete`): **soft delete** —
  `IsActive = false`, o registro nunca é removido do banco. A razão é
  explícita no código: hard delete quebraria FKs de dados históricos
  (screenshots, logs de app-focus, etc. — todo o histórico de monitoramento
  referencia o `UserId`).
- `PUT me/avatar` (qualquer usuário autenticado, sem permissão extra): upload
  multipart de foto de perfil, salva em
  `wwwroot/uploads/profile-photos/{guid}.{ext}` no disco local do servidor
  (não em object storage) e grava a URL relativa em `User.AvatarUrl`.
  Aceita também `AvatarUrl` direto no DTO sem arquivo (ex.: link externo).
- `GET roles`: lista os papéis disponíveis para seleção em UI — SuperAdmin/
  TenantAdmin veem `UserRoles.All` (inclui `SuperAdmin`), qualquer outro role
  vê só `UserRoles.TenantLevel` (sem `SuperAdmin`), para não deixar um
  Manager, por exemplo, criar outro SuperAdmin pela UI.

## 3. Diferença entre `UserService` (tenant) e `SuperAdminUserService`

Existe um segundo serviço, `SuperAdminUserService` (usado por
`SuperAdminUsersController`), que opera **sem** o filtro de
`OrganizationId` — permite ao SuperAdmin de plataforma ver/gerenciar usuários
de qualquer tenant. Ver [`superadmin-users.md`](superadmin-users.md).

## 4. Referência de endpoints

Base: `api/User`. Todos `[Authorize]`.

| Método | Rota | Permissão | Uso |
|---|---|---|---|
| `POST` | `/` | `UsersCreate` | Cria usuário na organização do token |
| `GET` | `/` | `UsersView` | Lista usuários da organização |
| `GET` | `/{id}` | `UsersView` | Usuário por id |
| `PUT` | `/{id}` | `UsersEdit` | Atualização parcial |
| `DELETE` | `/{id}` | `UsersDelete` | Soft delete (`IsActive = false`) |
| `PUT` | `/me/avatar` | — (qualquer autenticado) | Upload/definição de avatar |
| `GET` | `/roles` | — (qualquer autenticado) | Papéis disponíveis para o role do chamador |

## 5. Relação com outros módulos

- `OrganizationId` vincula todo usuário a uma [`register-organization.md`](register-organization.md).
- `TeamMemberships`/`DepartmentMemberships` são usados por
  [`teams.md`](teams.md) e [`departments.md`](departments.md).
- `Role` determina elegibilidade para ser gestor de Team/Department e é a
  base do sistema de permissões em [`authorization-permissions.md`](authorization-permissions.md).
- Toda ação aqui gera entrada de auditoria — ver [`audit-logs.md`](audit-logs.md).
