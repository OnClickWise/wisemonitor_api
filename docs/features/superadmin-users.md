# SuperAdmin — Gestão de Usuários (cross-tenant)

Diferente do módulo `User`/`UserController` normal (que opera dentro do
tenant do usuário logado, ver `docs/features/users.md`), este módulo permite
a um SuperAdmin listar, editar, excluir, bloquear/desbloquear e "derrubar"
sessões de **qualquer usuário de qualquer organização** — inclusive criar
novos SuperAdmins.

## 1. Endpoints (`SuperAdminUsersController`, base `api/super-admin/users`)

| Método | Rota | Permissão | Uso |
|---|---|---|---|
| `GET` | `/` | `UsersView` | Lista cross-tenant com filtros (busca, `OrganizationId`, papel, ativo/inativo) |
| `POST` | `/` | `UsersCreate` | Cria um novo SuperAdmin de plataforma (não um usuário de tenant) |
| `GET` | `/{id}` | `UsersView` | Detalhe de um usuário |
| `PATCH` | `/{id}` | `UsersEdit` | Atualiza nome, papel, status ativo |
| `DELETE` | `/{id}` | `UsersDelete` | Remove um usuário (bloqueado para SuperAdmins, ver seção 3) |
| `POST` | `/{id}/force-logout` | `UsersEdit` | "Invalida todas as sessões" — ver seção 4, comportamento real é limitado |
| `POST` | `/{id}/unlock` | `UsersEdit` | Reativa um usuário (`IsActive = true`) |
| `GET` | `/{id}/sessions` | `UsersView` | Lista sessões ativas — ver seção 4, hoje retorna sessões da organização, não do usuário |

## 2. Criação de SuperAdmin (`CreateSuperAdminAsync`)

`POST /` **não cria um usuário comum** — sempre cria com
`Role = UserRoles.SuperAdmin` e `IsSuperAdmin = true`, sem vínculo a nenhuma
`OrganizationId` (fica `null`). Valida e-mail único globalmente antes de
criar, faz hash da senha com BCrypt, e registra auditoria
(`action: "superadmin_create"`). Não há um segundo fator de confirmação (ex.:
convite por e-mail) — o SuperAdmin que chama este endpoint já define a
senha inicial diretamente no payload (`CreateSuperAdminDTO.Password`).

## 3. Exclusão protege SuperAdmins entre si

`DeleteAsync` lança `InvalidOperationException` se `user.IsSuperAdmin` for
verdadeiro — ou seja, este endpoint **não pode ser usado para remover outro
SuperAdmin** (proteção contra um SuperAdmin comprometido ou mal-intencionado
apagar os demais). Para usuários normais de tenant, a exclusão é direta
(`_context.Users.Remove`), sem soft-delete, e é registrada em auditoria antes
da remoção efetiva.

## 4. `force-logout` e `sessions` — funcionalidade parcial

Dois pontos importantes a considerar antes de confiar nestes endpoints como
controle de segurança real:

- **`ForceLogout`/`InvalidateAllSessionsAsync`**: o código **não invalida
  nenhum token JWT existente**. Ele consulta `LiveSessions` filtrando pela
  organização do usuário (não há uma tabela de sessões por usuário
  individual), mas o resultado dessa consulta (`sessions`) sequer é usado —
  a única coisa que a função realmente faz é gravar uma entrada de auditoria
  (`action: "user_force_logout"`). Como a autenticação é feita por JWT
  stateless (sem uma blacklist de tokens revogados verificada a cada
  requisição), **um usuário "deslogado à força" continua com um token JWT
  válido até ele expirar naturalmente**. Se o objetivo é realmente revogar
  acesso imediato, isso exigiria um mecanismo adicional (blacklist de JTI,
  ou reduzir drasticamente o tempo de vida do token) que não existe hoje.
- **`GetActiveSessionsAsync`**: apesar do nome sugerir "sessões deste
  usuário", a query filtra `LiveSessions` por `OrganizationId` do usuário —
  ou seja, retorna sessões de **toda a organização**, não sessões
  específicas deste usuário (não existe granularidade por usuário no modelo
  `LiveSession` atual).

## 5. Relação com outros módulos

- Reaproveita `UserUpdateDTO` do módulo `User` normal (`docs/features/users.md`)
  para a atualização de campos — mesmo shape de dados, contexto diferente
  (cross-tenant vs. dentro do próprio tenant).
- `SuperAdminTenantsController.GetUsers` (`docs/features/superadmin-tenants.md`)
  devolve o mesmo `SuperAdminUserResponseDTO` usado aqui, só que já
  pré-filtrado por um tenant específico.
