# SuperAdmin — Gestão de Tenants

Permite a um SuperAdmin de plataforma (não vinculado a nenhuma organização
específica) gerenciar todos os tenants (organizações) do sistema: criar,
suspender, alterar plano, resetar senha do admin, "entrar como" (impersonate)
e configurar branding — tudo cross-tenant, contornando o isolamento normal
de `OrganizationId` que rege o resto do sistema.

Ver [`docs/features/authorization-permissions.md`](authorization-permissions.md)
para como `[SuperAdminOnly]` + `[HasPermission(...)]` funcionam juntos —
todas as ações aqui exigem ambos.

## 1. Endpoints (`SuperAdminTenantsController`, base `api/super-admin/tenants`)

| Método | Rota | Permissão | Uso |
|---|---|---|---|
| `GET` | `/` | `TenantsView` | Lista paginada/filtrada (busca por nome/email do admin, plano, status, intervalo de criação, ordenação) |
| `GET` | `/{id}` | `TenantsView` | Detalhe de um tenant |
| `POST` | `/` | `TenantsCreate` | Cria organização + usuário admin em uma única operação |
| `PATCH` | `/{id}` | `TenantsEdit` | Atualiza campos (nome, plano, limites, notas internas, e-mail de cobrança, trial) |
| `DELETE` | `/{id}` | `TenantsDelete` | Remove o tenant **permanentemente** (ver seção 3 — irreversível e sem cascade explícito no service) |
| `POST` | `/{id}/suspend` | `TenantsSuspend` | Suspende o tenant (com motivo e tipo) |
| `POST` | `/{id}/activate` | `TenantsSuspend` | Reativa um tenant suspenso |
| `PUT` | `/{id}/plan` | `TenantsEdit` | Troca o plano, com nota de billing opcional |
| `GET` | `/{id}/stats` | `TenantsView` | Estatísticas agregadas (usuários, dispositivos, screenshots, equipes, departamentos) |
| `GET` | `/{id}/users` | `TenantsView` | Lista paginada de usuários do tenant |
| `POST` | `/{id}/reset-admin-password` | `TenantsEdit` | Gera senha temporária para o admin do tenant |
| `POST` | `/{id}/impersonate` | `TenantsEdit` | Gera um JWT válido por 30 min para acessar como se fosse o admin do tenant |
| `GET` | `/{id}/branding` | `TenantsView` | Lê configuração de marca (logo, cores, fonte) |
| `PUT` | `/{id}/branding` | `TenantsEdit` | Atualiza configuração de marca |

## 2. Criação de tenant (`CreateAsync`)

Cria, em duas etapas sequenciais (não em uma transação explícita — ver nota
abaixo): primeiro a `Organization` (nome, plano, status derivado de
`TrialEndsAt.HasValue ? "Trial" : "Active"`, limites de usuários/storage,
notas internas), depois o `User` admin (senha com `BCrypt.HashPassword`,
`Role = TenantAdmin`), e por fim liga `org.AdminUserId` ao usuário recém-criado
com um terceiro `SaveChangesAsync`. Todas as etapas registram uma entrada de
auditoria (`action: "tenant_create"`) só ao final. Valida antecipadamente que
o e-mail do admin não está em uso por ninguém na plataforma (não só dentro do
tenant, já que e-mail é único globalmente no sistema).

**Nota de robustez**: como não há uma transação de banco explícita
envolvendo as três chamadas a `SaveChangesAsync`, uma falha entre a criação
da organização e a criação do usuário deixaria uma `Organization` órfã sem
`AdminUserId` — cenário raro, mas real, já que o `EF Core` por padrão não
agrupa `SaveChangesAsync` sucessivos numa transação automática.

## 3. Exclusão de tenant é imediata e sem verificação de dependências

`DeleteAsync` simplesmente encontra a `Organization` e chama
`_context.Organizations.Remove(org)` + `SaveChangesAsync`, registrando a
auditoria **antes** de remover (o log é gravado com o nome do tenant que já
não vai mais existir depois). Não há um passo de soft-delete, confirmação em
duas etapas, ou verificação explícita no service de quantos registros
dependentes (usuários, devices, screenshots, etc.) seriam afetados — o
comportamento real de cascade depende inteiramente da configuração de
`DeleteBehavior` do EF Core nas migrations (fora do escopo deste documento,
mas vale conferir antes de expor este botão numa UI sem confirmação extra).

## 4. Suspensão vs. exclusão

`SuspendAsync` é reversível: marca `Status = "Suspended"`, grava `SuspendedAt`,
`SuspendReason`, `SuspensionType`, `SuspendUntil` (suspensão com prazo
definido é suportada via esse campo, mas não há um job/scheduler encontrado
no código que reative automaticamente ao atingir `SuspendUntil` — a
reativação parece ser sempre manual via `ActivateAsync`). Lança
`InvalidOperationException` (→ `400`) se o tenant já estiver suspenso.

## 5. Impersonação

`GenerateImpersonationTokenAsync` gera um JWT normal via `IJwtService.GenerateToken(org.AdminUser)`
— **é literalmente o mesmo token que o admin do tenant receberia de um login
normal**, não um token com escopo/claims reduzidos ou marcado como
"sessão de impersonação". O único controle de expiração é o campo
`ExpiresIn = 1800` retornado na resposta (30 min) para orientação do
cliente/frontend, mas isso depende do `IJwtService` de fato honrar essa
janela na expiração real do token (`exp` claim) — não há um mecanismo
adicional de revogação/blacklist de token de impersonação encontrado no
código. A ação fica registrada em auditoria (`tenant_impersonate`).

## 6. Reset de senha do admin

Gera uma senha temporária local (`Guid.NewGuid().ToString("N")[..12]` — 12
caracteres hexadecimais, não uma senha com caracteres especiais/maiúsculas
garantidas), faz o hash com BCrypt e retorna em texto plano na resposta
(`{ TemporaryPassword: "..." }`) para o SuperAdmin repassar ao cliente por
fora do sistema. Lança erro se o tenant não tiver `AdminUser` configurado.

## 7. Branding

`BrandingDTO`/`UpdateBrandingDTO`: `LogoUrl`, `DisplayName`, `PrimaryColor`,
`SecondaryColor`, `AccentColor`, `FontFamily` — persistidos como colunas
diretas em `Organization` (`BrandingLogoUrl`, etc., não uma tabela separada).
`GetBrandingAsync` retorna `null` se **todos** os campos de branding forem
nulos (tenant nunca customizou nada) em vez de um objeto com campos vazios —
distinção que o consumidor da API precisa tratar.

## 8. Relação com outros módulos

- `GetUsersAsync` reaproveita o mesmo formato `SuperAdminUserResponseDTO` de
  `docs/features/superadmin-users.md`.
- `GetStatsAsync` agrega contagens de `Screenshots`, `Teams`, `Departments`,
  `Devices` — mesmas entidades documentadas em seus próprios módulos.
  `TeamCount`/`DepartmentCount` na listagem paginada (`GetAllAsync`) são
  computados via um `GroupBy` separado sobre `Teams` fora do `Include`
  principal — otimização para não trazer todas as equipes de todos os
  tenants via `Include(o => o.Teams)` (que não existe aqui) só para contar.
