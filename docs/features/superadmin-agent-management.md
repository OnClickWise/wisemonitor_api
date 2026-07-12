# SuperAdmin — Gestão de Versões do Agente Desktop

Permite a um SuperAdmin publicar novas versões do agente desktop no
catálogo do banco de dados (`AgentVersion`), mudar o canal de distribuição,
forçar atualização e ver a distribuição de versões instaladas entre os
dispositivos. **Importante**: este catálogo é gerido separadamente do que o
agente desktop realmente consulta — ver
[`docs/features/agent-distribution.md`](agent-distribution.md) seção 3 para
o detalhe completo dessa desconexão. Em resumo: publicar uma versão aqui
**não** muda automaticamente o que `GET /api/agent/version` retorna para os
agentes rodando em campo.

## 1. Endpoints (`SuperAdminAgentController`, base `api/super-admin/agent`)

Nenhuma ação tem `[HasPermission(...)]` explícito — depende só do
`[SuperAdminOnly]` da base (mesma observação de inconsistência de
`docs/features/authorization-permissions.md`).

| Método | Rota | Uso |
|---|---|---|
| `GET` | `/versions` | Lista todas as versões publicadas, mais recentes primeiro |
| `POST` | `/versions` | Publica uma nova versão (`400` se a string de versão já existir) |
| `PATCH` | `/versions/{id}` | Muda o canal (Stable/Beta/Alpha/Deprecated) |
| `POST` | `/versions/{id}/force-update` | Marca a versão como força-atualização e conta dispositivos afetados |
| `GET` | `/stats` | Distribuição percentual de versões instaladas, por `Device.AgentVersion` |

## 2. `AgentVersion` — modelo

`Models/AgentVersion.cs`: `Version` (semver, ex. "2.1.0"), `Channel`
("Stable"/"Beta"/"Alpha"/"Deprecated"), `ReleaseNotes`, `Checksum`,
`ForceUpdate`, `MinimumVersion` (versão mínima aceita, presumivelmente para
o agente comparar e recusar rodar/forçar update — a lógica de comparação em
si não está neste módulo, ficaria do lado do agente desktop), URLs e
checksums **por plataforma** (`WindowsDownloadUrl`/`MacOsDownloadUrl`/`LinuxDownloadUrl`
+ respectivos `*Checksum`), `IsActive`, `PublishedByAdminId`, `PublishedAt`.

Note que isto é mais completo que `AgentSettings` (usado pelo endpoint
público) — suporta múltiplas plataformas com checksums individuais, várias
versões coexistindo com canais diferentes, e histórico de quem publicou —
mas, como mencionado, nada disso alimenta o que o agente público consulta
hoje.

## 3. Publicação de versão

`PublishVersionAsync` valida unicidade do campo `Version` (não pode publicar
"2.1.0" duas vezes) antes de inserir, registra auditoria
(`action: "agent_version_publish"`) e retorna o DTO mapeado. Não há
validação de formato semver no código (`Version` é só uma string,
qualquer valor passa desde que não repita).

## 4. `UpdateChannelAsync` — mover para "Deprecated" desativa automaticamente

Ao mudar o canal para `"Deprecated"`, o service automaticamente também seta
`IsActive = false` — é o único efeito colateral automático de mudança de
canal; mudar para qualquer outro canal não altera `IsActive`.

## 5. `ForceUpdateAsync` — conta dispositivos, mas não dispara nada

Marca `version.ForceUpdate = true` e conta quantos `Device`s seriam afetados
(opcionalmente filtrando por `TargetOrganizationId`), retornando essa
contagem (`AffectedDevices`) como resposta. **Não há um mecanismo de push
para o agente** — a contagem é só informativa/estimativa para o SuperAdmin
ver o alcance da ação; o agente descobriria isso (se o catálogo estivesse
conectado, o que hoje não está — ver seção introdutória) só na próxima vez
que consultasse `/api/agent/version`.

## 6. Distribuição de versões instaladas

`GetVersionDistributionAsync` agrupa `Device.AgentVersion` (uma coluna livre
gravada pelo próprio agente ao se conectar — não uma FK para `AgentVersion.Id`)
e calcula percentual sobre o total de dispositivos. Dispositivos sem versão
registrada aparecem agrupados como `"unknown"`.
