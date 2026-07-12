# Distribuição do agente desktop (instalador + versão)

Este módulo serve o instalador do agente desktop (`wisemonitor_desktop`) e
informa qual é a versão "atual" para os clientes que já estão rodando. É a
contraparte pública de `docs/features/superadmin-agent-management.md` —
**mas leem de fontes de dados diferentes** (ver seção 3, é o ponto mais
importante deste documento).

## 1. Endpoints públicos (`AgentController`, base `api/agent`)

Nenhuma ação tem `[Authorize]` — são endpoints públicos por design (o
instalador precisa ser baixável antes mesmo de existir um usuário logado).

| Método | Rota | Uso |
|---|---|---|
| `GET` | `/download` | Retorna JSON com a URL de download, versão, nome do arquivo e plataforma (`AgentDownloadDTO`) |
| `GET` | `/installer` | Serve o binário do instalador (proxy de URL remota OU streaming local em chunks) |
| `GET` | `/version` | Retorna versão atual + flag de atualização forçada + URL de download (`AgentVersionDTO`) — é isso que o agente desktop consulta para saber se deve se atualizar |

## 2. `/installer` — dois modos de servir o arquivo

`AgentService`/`AgentController.Installer()` decide entre dois caminhos, com
base em `AgentSettings.DirectDownloadUrl`:

1. **Proxy de URL remota** (se `DirectDownloadUrl` estiver configurada): a
   API faz um `GET` para essa URL (ex.: GitHub Releases, GCS) com
   `ResponseHeadersRead` e repassa o corpo direto para a resposta, com o
   `Content-Type` e `Content-Disposition` corretos. Evita ter que empacotar
   o instalador (~dezenas de MB) dentro da imagem Docker da API.
2. **Streaming local** (fallback, sem `DirectDownloadUrl`): serve um arquivo
   de `wwwroot/downloads/agent/{PlatformFolder}/v{CurrentVersion}/{FileName}`
   via `FileStream` copiado diretamente para `Response.Body`, **sem**
   `Content-Length` — isso é proposital: o comentário no código explica que
   o Cloud Run tem um limite de 32MB para respostas *bufferizadas*; omitir
   `Content-Length` força *chunked transfer encoding*, que não tem esse
   limite.

## 3. Desconexão real entre `AgentSettings` (estático) e `AgentVersion` (banco)

Este é o ponto mais importante deste módulo: **`GET /api/agent/version`
(o que o agente desktop realmente consulta) não lê da tabela `AgentVersions`
no banco** — lê de `Configs/AgentSettings`, um objeto de configuração estático
(`appsettings.json`/variáveis de ambiente: `AppBaseUrl`, `BaseDownloadUrl`,
`DirectDownloadUrl`, `CurrentVersion`, `ForceUpdate`, `PlatformFolder`,
`FileName`).

Enquanto isso, `SuperAdminAgentController`/`SuperAdminAgentService`
(`docs/features/superadmin-agent-management.md`) gerenciam registros
`AgentVersion` **no Postgres**, com canais (Stable/Beta/Alpha/Deprecated),
checksum, `ForceUpdate` por versão, etc. — mas **nada nesse fluxo grava de
volta em `AgentSettings`**. Ou seja, hoje um SuperAdmin pode "publicar uma
nova versão" via `POST /api/super-admin/agent/versions` e isso fica
registrado no banco (aparece em `GetVersions`/`GetVersionStats`), mas os
agentes desktop **não vão saber disso** até alguém atualizar manualmente
`AgentSettings.CurrentVersion`/`ForceUpdate` na configuração da API (ex.:
variável de ambiente + redeploy). As duas telas (distribuição de versão via
SuperAdmin, e o que o agente realmente recebe) não estão conectadas — vale
tratar isso como uma lacuna conhecida se o objetivo do painel SuperAdmin de
versões é realmente controlar o rollout.

## 4. Configuração (`Configs/AgentSettings.cs`)

| Campo | Descrição | Padrão |
|---|---|---|
| `AppBaseUrl` | Base usada para montar a URL de fallback do `/installer` | `""` |
| `BaseDownloadUrl` | Base usada para montar a `DownloadUrl` retornada por `/version` | `""` |
| `DirectDownloadUrl` | Se definida, `/installer` faz proxy dela em vez de servir arquivo local | `""` |
| `CurrentVersion` | Versão "atual" reportada por `/version` e usada no caminho de arquivo local | `"1.0.0"` |
| `ForceUpdate` | Flag repassada tal qual em `AgentVersionDTO.ForceUpdate` | `false` |
| `PlatformFolder` | Subpasta de plataforma (`win-x64`, etc.) | `"win-x64"` |
| `FileName` | Nome do arquivo do instalador | `"WiseMonitorAgentSetup.exe"` |
