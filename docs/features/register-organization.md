# Registro de Organização

Cria uma nova organização (tenant) junto com seu primeiro usuário
administrador, e permite consultar/editar os dados básicos da organização do
usuário logado. É o ponto de entrada para qualquer empresa nova no sistema —
sem isso, não existe tenant para nenhum outro módulo operar dentro.

## 1. Registro (`POST /api/RegisterOrganization`)

Sem `[Authorize]` — precisa ser público, já que quem chama ainda não tem
conta. Fluxo em `OrganizationService.RegisterOrganizationAsync`:

1. Normaliza nome da organização (trim) e e-mail do admin (trim +
   lowercase).
2. Valida unicidade: nome de organização (case-insensitive) e e-mail do admin
   não podem já existir. **Ambas as validações retornam a mesma mensagem
   genérica** ("Não foi possível criar a organização.") — decisão deliberada
   para não vazar se um nome de empresa ou e-mail específico já está em uso.
3. Hash da senha com BCrypt, `workFactor: 12` (mais caro computacionalmente
   que o default de 10/11 usado em outros pontos do sistema, ex.:
   `UserService.CreateUserAsync` — inconsistência menor, não um bug, mas vale
   uniformizar se a política de custo de hashing for revisada).
4. Cria `Organization` + `User` (role `"admin"`) **na mesma transação**
   (`BeginTransactionAsync`) — se qualquer `SaveChangesAsync` falhar, tudo é
   revertido; não fica uma organização órfã sem admin.
5. Retorna apenas IDs e e-mail (`RegisterOrganizationResultDTO`) — nunca o
   hash de senha nem o token (o cliente precisa fazer login separadamente
   depois, via [`auth.md`](auth.md)).

**Validação de senha forte**: `RegisterOrganizationDTO.AdminPassword` usa o
atributo customizado `[StrongPassword]` (`Validators/StrongPassword.cs`) além
do `[MinLength(6)]` — a única entrada de senha no sistema com essa checagem
adicional (o `UserCreateDTO`/`UserUpdateDTO` usados por
[`users.md`](users.md) não têm esse validador).

## 2. Organização do usuário logado (`/me`)

- `GET me` (`[Authorize]`): busca a organização pelo `orgId` do token
  (`GetByIdWithAdminAsync`, que inclui `Users`), acha o primeiro usuário com
  `Role == "admin"` (comparação de string literal — não usa
  `UserRoles.Normalize`/`TenantAdmin`, então se o role canônico mudar de
  case ou nome essa busca pode silenciosamente deixar de encontrar o admin) e
  retorna um `OrganizationWithAdminDTO`.
- `PUT me` (`[Authorize]`): atualiza `Name`/`LegalName`/`Cnpj`. Qualquer
  usuário autenticado da organização pode chamar — **não há checagem de role
  aqui** (diferente de, por exemplo, `TenantBrandingController.Update`, que
  exige `TenantAdmin`). Atualizar o `Nome` também sobrescreve
  `BrandingDisplayName` com o mesmo valor — os dois campos ficam acoplados
  nesse endpoint.

## 3. Modelo de dados (`Organization`)

Campos relevantes além dos já citados: `Plan` (Free/Basic/Pro/Enterprise),
`Status` (Active/Suspended/Cancelled/Trial) + `SuspendedAt`/`SuspendReason`/
`SuspensionType`/`SuspendUntil` (suspensão administrativa, gerenciada pelo
SuperAdmin — ver [`superadmin-tenants.md`](superadmin-tenants.md)), limites de
plano (`MaxUsers`/`MaxDevices`/`StorageLimitGb`), branding white-label
(`BrandingLogoUrl`/`BrandingDisplayName`/`BrandingPrimaryColor`/etc. — ver
[`tenant-branding.md`](tenant-branding.md)) e métricas cacheadas
(`CachedUserCount`/`CachedDeviceCount`/`CachedStorageGb`/`LastActivityAt`,
atualizadas por processos externos a este controller).

## 4. Referência de endpoints

Base: `api/RegisterOrganization`.

| Método | Rota | Auth | Uso |
|---|---|---|---|
| `POST` | `/` | Pública | Cria organização + admin |
| `GET` | `/me` | JWT | Dados da organização do usuário logado + resumo do admin |
| `PUT` | `/me` | JWT (qualquer role) | Atualiza nome/razão social/CNPJ |

## 5. Relação com outros módulos

- É o único jeito de criar uma `Organization` — todo o resto do sistema
  (Users, Teams, Departments, Devices, etc.) depende de um `OrganizationId`
  criado aqui.
- Branding visual é um módulo separado ([`tenant-branding.md`](tenant-branding.md))
  mesmo compartilhando a mesma entidade `Organization`.
- Suspensão/gestão de plano é feita pelo SuperAdmin, não por este controller
  (ver [`superadmin-tenants.md`](superadmin-tenants.md)).
