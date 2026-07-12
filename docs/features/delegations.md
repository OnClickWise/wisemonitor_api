# Delegações (Delegation)

Permite que um gestor delegue temporariamente parte do seu escopo de
permissões/acesso (equipes, departamentos, permissões específicas) para
outro usuário — útil para cobertura durante férias/ausências, sem precisar
trocar o role permanente de ninguém.

## 1. Modelo de dados

`Delegation`: `OrganizationId`, `DelegatorId` (quem delega) + `DelegateId`
(quem recebe), `Scope` (**string** contendo um JSON serializado de
`DelegationScopeDTO`: `TeamIds`, `DepartmentIds`, `Permissions` — o escopo não
é modelado como colunas relacionais, é um blob JSON dentro de uma coluna de
texto), `StartDate`/`EndDate`, `IsActive`, `Reason` opcional, `CreatedAt`,
`RevokedAt`/`RevokedBy` (preenchidos na revogação).

**Observação**: guardar o escopo como JSON em texto significa que não há
integridade referencial nem índice sobre `TeamIds`/`DepartmentIds` dentro do
escopo — se uma equipe referenciada for excluída, o `Guid` órfão continua
"vivo" dentro do JSON da delegação sem nenhum aviso.

## 2. Validações na criação

`DelegationService.CreateAsync` valida, antes de gravar:
- `EndDate` precisa ser posterior a `StartDate`.
- `EndDate` precisa estar no futuro (`> DateTime.UtcNow`) — não é possível
  criar uma delegação já expirada.
- `DelegateId` precisa ser um usuário existente **da mesma organização** do
  delegante.

Não há validação equivalente sobre `DelegatorId` pertencer à organização —
ele vem direto do claim do usuário autenticado (`User.GetUserId()`), então
isso é garantido implicitamente pela autenticação, não checado explicitamente
no service.

## 3. Expiração

`ExpireOutdatedAsync` varre todas as delegações `IsActive == true` cujo
`EndDate` já passou e marca `IsActive = false` em lote. **Não há chamada
automática/agendada para este método em nenhum ponto do código pesquisado**
— ele existe na interface e no service, mas parece depender de algum job
externo (cron, Hangfire, etc.) não presente neste repositório, ou de uma
chamada manual ainda não conectada. Na prática, hoje, uma delegação vencida
continua com `IsActive = true` no banco até que algo chame este método
explicitamente — qualquer código que **use** o escopo de uma delegação para
checar permissão deveria também comparar `EndDate` contra a data atual, não
confiar isoladamente em `IsActive`.

## 4. Revogação

`DELETE /{id}`: marca `IsActive = false`, grava `RevokedAt` e `RevokedBy`
(quem revogou, não necessariamente o delegante original — qualquer usuário
com a permissão `DelegationsCreate` pode revogar qualquer delegação da
organização). Não deleta o registro — fica como histórico.

## 5. Referência de endpoints

Base: `api/delegations`. Todos `[Authorize]`.

| Método | Rota | Permissão | Uso |
|---|---|---|---|
| `GET` | `/` | `DelegationsView` | Lista todas as delegações da organização |
| `GET` | `/my` | — (qualquer autenticado) | Delegações onde o usuário logado é delegante OU delegado |
| `POST` | `/` | `DelegationsCreate` | Cria delegação (delegante = usuário do token) |
| `DELETE` | `/{id}` | `DelegationsCreate` | Revoga (usa a mesma permissão de criação, não uma dedicada) |

## 6. Relação com outros módulos

- `Scope.Permissions` presumivelmente referencia os mesmos nomes de
  permissão de [`authorization-permissions.md`](authorization-permissions.md),
  mas a aplicação efetiva desse escopo (ex.: um handler de autorização que
  amplie os direitos do delegado durante a janela ativa) não está neste
  service — ele só persiste e consulta o registro; não foi encontrado no
  código lido código que *aplique* a delegação em tempo de autorização de
  requests.
- `DelegatorId`/`DelegateId` são [`users.md`](users.md).
- `TeamIds`/`DepartmentIds` do escopo referenciam
  [`teams.md`](teams.md)/[`departments.md`](departments.md) sem FK real
  (só IDs dentro do JSON).
