# Departamentos (Department)

Camada de agrupamento acima de `Team` — um departamento pode conter várias
equipes e tem seu próprio quadro de membros com papéis internos
(Diretor/Supervisor/Membro/Convidado), independente dos papéis de sistema
(`UserRoles`).

## 1. Modelo de dados

`Department`: `OrganizationId`, `Name`, `Description`, `IsActive`. Tem
`Members` (`DepartmentMember`) e `Teams` (relação reversa de `Team.DepartmentId`).

`DepartmentMember`: `DepartmentId` + `UserId` + `MemberRole` (string livre,
mas restrita por validação a `DIRECTOR`/`SUPERVISOR`/`MEMBER`/`GUEST` — o
enum `DepartmentMemberRole` existe em `Models/Enums/DepartmentMemberRole.cs`
mas **não é o tipo real da coluna**; `DepartmentMember.MemberRole` é
`string`, o enum parece ter sido criado como referência e nunca
efetivamente adotado como tipo de dado) + `JoinedAt`.

Papéis dentro do departamento são conceitualmente diferentes do `Role` de
sistema do usuário (ex.: um `Employee` pode ser `DIRECTOR` de um
departamento — são namespaces de autorização separados).

## 2. Regra de negócio: um único diretor por departamento

`AddMemberAsync`, `UpdateMemberRoleAsync` e `SetDirectorAsync` compartilham a
mesma regra: só pode haver um membro com `MemberRole == "DIRECTOR"` por vez.
Tentar definir um segundo diretor lança `InvalidOperationException` **a menos
que** o chamador passe `ForceReplaceDirector`/`ForceReplace = true` no DTO —
nesse caso o diretor anterior é rebaixado para `MEMBER` automaticamente (não
removido do departamento, só perde o papel).

Essa regra é reimplementada de forma independente (código quase idêntico)
em três lugares (`AddMemberAsync`, `UpdateMemberRoleAsync`,
`SetDirectorAsync`) — funciona, mas é candidato a extração para um método
privado único se o comportamento precisar mudar no futuro.

## 3. Endpoints e regras de acesso

Todos exigem `[HasPermission(Permissions.Departments*)]` (ver
[`authorization-permissions.md`](authorization-permissions.md)) e geram
entrada de auditoria via `IAuditService` (exceto os `GET`s).

- `GET /`, `GET /{id}`: lista/detalhe com membros e contagem de equipes.
- `GET /{id}/overview`: visão agregada — diretor, supervisores, resumo de
  cada equipe (nome, contagem de membros, nome do gestor) e **todas** as
  jornadas de trabalho da organização (`Schedules`, não filtradas por
  departamento — parece uma lista de referência para a UI popular um
  seletor, não jornadas "do" departamento especificamente).
- `GET /{id}/members`, `GET /{id}/supervisors`: listas filtradas por papel.
- `POST /`: cria o departamento e, se `DirectorUserId` vier preenchido, já
  adiciona esse usuário como `DIRECTOR`.
- `PUT /{id}`: patch parcial de nome/descrição/`IsActive`.
- `DELETE /{id}`: hard delete.
- `POST /{id}/members`, `DELETE /{id}/members/{userId}`,
  `PATCH /{id}/members/{userId}/role`, `PUT /{id}/director`: gestão de
  membros e papéis, com a regra de diretor único descrita acima. `PATCH` de
  papel retorna `409 Conflict` (não `400`) quando a troca de diretor não é
  forçada e já existe um.

## 4. Referência de endpoints

Base: `api/departments`. Todos `[Authorize]` + `[HasPermission]`.

| Método | Rota | Permissão | Uso |
|---|---|---|---|
| `GET` | `/` | `DepartmentsEdit` | Lista departamentos |
| `GET` | `/{id}` | `DepartmentsEdit` | Detalhe |
| `GET` | `/{id}/overview` | `DepartmentsEdit` | Visão agregada (diretor, supervisores, equipes, jornadas) |
| `GET` | `/{id}/members` | `DepartmentsEdit` | Membros |
| `GET` | `/{id}/supervisors` | `DepartmentsEdit` | Só supervisores |
| `POST` | `/` | `DepartmentsCreate` | Cria departamento |
| `PUT` | `/{id}` | `DepartmentsEdit` | Atualiza |
| `DELETE` | `/{id}` | `DepartmentsDelete` | Remove (hard delete) |
| `POST` | `/{id}/members` | `DepartmentsEdit` | Adiciona/atualiza papel de membro |
| `DELETE` | `/{id}/members/{userId}` | `DepartmentsEdit` | Remove membro |
| `PATCH` | `/{id}/members/{userId}/role` | `DepartmentsEdit` | Troca papel de um membro |
| `PUT` | `/{id}/director` | `DepartmentsEdit` | Define/substitui o diretor |

Nota: o uso de `DepartmentsEdit` (não um permission específico de leitura)
para todos os `GET`s significa que, no sistema de permissões atual, ver
detalhes de um departamento exige a mesma permissão que editá-lo — não há
uma permissão "somente leitura" separada para este módulo.

## 5. Relação com outros módulos

- `Teams` referenciam `Department` via `Team.DepartmentId` (ver
  [`teams.md`](teams.md)).
- Membros são [`users.md`](users.md).
- `GetOverviewAsync` lê jornadas de trabalho de
  [`work-schedules.md`](work-schedules.md), embora sem filtrar por
  departamento.
