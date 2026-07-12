# Equipes (Teams)

Agrupamento de usuários sob um gestor único, opcionalmente vinculado a um
`Department` e a uma `WorkSchedule` padrão.

## 1. Modelo de dados

`Team`: `OrganizationId`, `DepartmentId` (opcional — uma equipe pode existir
sem departamento), `DefaultWorkScheduleId` (opcional, jornada padrão herdada
pelos membros que não têm jornada individual — ver
[`work-schedules.md`](work-schedules.md)), `Name`, `Description`,
`ManagerId` (obrigatório, `User` não-nulo). `TeamMember` é a tabela de junção
(`TeamId` + `UserId` + `JoinedAt`), sem papel adicional dentro da equipe (ao
contrário de `DepartmentMember`, que tem `MemberRole`).

## 2. Regra de negócio: quem pode ser gestor de equipe

`TeamService.CreateAsync` valida que `ManagerId` aponta para um usuário cujo
role normalizado (`UserRoles.Normalize`) esteja em
`{ TenantAdmin, Director, Manager, Supervisor, ProjectManager, SuperAdmin }`
— um `Employee` ou `Client` não pode ser definido como gestor de equipe; a
tentativa lança exceção com a mensagem do cargo atual. Essa mesma checagem
**não é repetida** em `UpdateAsync` ao trocar de gestor (`ManagerId.HasValue`
branch) — lá só verifica se o usuário existe, não se o role é elegível. É uma
inconsistência real: dá para promover um `Employee` a gestor de equipe via
`PUT`, mas não via `POST`.

## 3. Endpoints e comportamento

- `POST /` : cria a equipe e, se `MemberIds` vier preenchido, adiciona cada
  membro (ignorando silenciosamente IDs que não existem na organização —
  `continue` sem erro).
- `GET /`: lista todas as equipes da organização com gestor, jornada padrão e
  membros (com `FullName`/`Role` de cada um), via `Include`s carregados
  antecipadamente (sem paginação).
- `GET /{id}`: **está com bug** — `TeamService.GetByIdAsync` lança
  `NotImplementedException` incondicionalmente. O endpoint existe no
  controller e sempre retorna erro 500 hoje.
- `PUT /{id}`: atualiza nome/descrição/jornada padrão, opcionalmente troca o
  gestor, e faz reconciliação de membros por diff (remove quem saiu da
  lista, adiciona quem é novo, ignora quem já estava) a partir do
  `MemberIds` do DTO.
- `DELETE /{id}`: remove a equipe (hard delete — diferente do soft delete
  usado em `User`). Lança `KeyNotFoundException` (→ deveria virar 404, mas o
  controller não tem um catch específico para essa exceção nas ações de
  update/delete/membros — hoje qualquer `KeyNotFoundException` não
  capturada nesses métodos vira erro 500 não tratado, não um 404).
- `POST /{id}/members/{userId}` : **está com bug** — `AddMemberAsync` lança
  `NotImplementedException` incondicionalmente. Não é possível adicionar
  membro a uma equipe já existente por este endpoint (só na criação, via
  `MemberIds` do `POST /`, ou via `PUT` reenviando a lista completa de
  membros).
- `DELETE /{id}/members/{userId}`: remove um membro específico da equipe
  (esse método está implementado corretamente).

## 4. Referência de endpoints

Base: `api/teams`. Todos `[Authorize]` (sem `[HasPermission]` granular — ao
contrário de Departments/Delegations, o controle de acesso aqui é só "estar
autenticado").

| Método | Rota | Uso | Observação |
|---|---|---|---|
| `POST` | `/` | Cria equipe | Valida role do gestor |
| `GET` | `/` | Lista equipes da organização | — |
| `GET` | `/{id}` | Busca por id | **Sempre lança erro (não implementado)** |
| `PUT` | `/{id}` | Atualiza equipe/membros/gestor | Não revalida role do novo gestor |
| `DELETE` | `/{id}` | Remove equipe | Hard delete |
| `POST` | `/{id}/members/{userId}` | Adiciona membro | **Sempre lança erro (não implementado)** |
| `DELETE` | `/{id}/members/{userId}` | Remove membro | — |

## 5. Relação com outros módulos

- `ManagerId`/`Members` apontam para [`users.md`](users.md).
- `DepartmentId` vincula a [`departments.md`](departments.md) (opcional).
- `DefaultWorkScheduleId` vincula a [`work-schedules.md`](work-schedules.md).
