# Dispositivos (Devices)

Representa cada máquina onde o agente desktop (`wisemonitor_desktop`) está
instalado e monitorando. É o "dono" de toda a telemetria — screenshots,
app-focus, teclado, segmentos de vídeo — todas essas entidades referenciam um
`DeviceId`.

Arquivos: `Controllers/DevicesController.cs`, `Services/DeviceService.cs`,
`Repositories/DeviceRepository.cs`, `Models/Device.cs`, `DTOs/DeviceCreateDTO.cs`,
`DTOs/DeviceDTO.cs`, `DTOs/DeviceUpdateDTO.cs`.

## Modelo de dados

`Device`: `Hostname`, `AgentEmail` (email do usuário logado no agente),
`Department` (string livre, não uma FK para `Department` — ver observação
abaixo), `IpAddress`, `IsOnline`, `AgentVersion`, `LastSeen`, `CreatedAt`,
`UpdatedAt`, `OrganizationId` (multi-tenant), `UserId` (`Guid?`, opcional —
correção manual de qual usuário está sendo monitorado nesta máquina, ver
"Correção manual do usuário" abaixo).

**Observação**: `Device.Id` é do tipo `Guid`, mas várias outras entidades que
referenciam um dispositivo (`Screenshot.DeviceId`, `VideoSegment.DeviceId`)
usam `string DeviceId` em vez de `Guid` — o desktop manda o mesmo valor como
texto nesses casos. `AppFocusEvent.DeviceId` já é `Guid`. Isso é uma
inconsistência de tipos entre módulos que existe hoje no código; não impede o
funcionamento (o desktop sempre serializa o `Guid` do dispositivo como string
onde for necessário), mas é uma armadilha para quem for escrever queries que
cruzem `Device` com `Screenshot`/`VideoSegment` diretamente por tipo.

## Endpoints

Base: `api/Devices`. Todos exigem `[Authorize]`. O `OrganizationId` **não** vem
de header nem de query — é lido do claim JWT `orgId` (`GetOrgId()` no
controller), então o cliente não pode escolher a organização, apenas o que o
token permitir.

| Método | Rota | Uso |
|---|---|---|
| `POST` | `/` | Registra um novo dispositivo (`DeviceCreateDTO`: Hostname, AgentEmail, Department, IpAddress) |
| `GET` | `/` | Lista todos os dispositivos da organização do usuário autenticado |
| `GET` | `/{id}` | Busca um dispositivo por id (escopado à organização) |
| `PUT` | `/{id}` | Atualiza Hostname/IpAddress/UserId (não atualiza AgentEmail/Department apesar do DTO aceitar — ver bug abaixo) |
| `DELETE` | `/{id}` | Remove um dispositivo |
| `POST` | `/heartbeat` | Batida de presença do agent (`DeviceHeartbeatDTO`) — sustenta `IsOnline`/`LastSeen`, ver seção "Presença" abaixo |
| `POST` | `/offline` | Aviso explícito de desligamento/logoff (`DeviceOfflineDTO`) — offline imediato |

## Presença (online/offline real)

Adicionado junto com a tela "ao vivo" via WebSocket
(`docs/features/live-monitoring.md`, seção "/ws/agent-stream"): antes disso,
`IsOnline`/`LastSeen` só mudavam quando algum outro módulo (screenshot,
app-focus) fazia upsert do device — ou seja, "online" só significava
"mandou telemetria recentemente", não "a máquina está ligada".

- **`POST /heartbeat`**: o agent chama isso a cada poucos segundos enquanto
  está ligado, monitorando ou não. `DeviceService.RegisterHeartbeatAsync`
  resolve o device por `DeviceId` e, se não achar, por `Hostname`
  (`IDeviceRepository.GetByHostnameAsync`, case-insensitive) — esse fallback é
  o que evita a máquina virar um device novo a cada reinstalação/perda do
  `session.json` local. Também empurra o estado para
  `ILiveMonitoringService.RegisterOrUpdateDevice(...)`, para o cache "ao vivo"
  em memória (que o painel consome via `/ws/monitor`) não expirar por TTL
  enquanto a máquina segue ligada sem monitoramento ativo.
- **`POST /offline`**: aviso explícito de desligamento/logoff — marca
  `IsOnline = false` na hora (`LastSeen` **não** é atualizado, ele continua
  registrando o último sinal de vida real) e chama
  `ILiveMonitoringService.MarkDeviceOffline(...)`.
- **`DevicePresenceSweeper`** (`BackgroundService`, varre a cada 15s):
  cobre os desligamentos em que nenhum aviso chega (queda de energia, bateria
  acabando, cabo removido, processo morto). Marca offline no banco
  (`IDeviceRepository.MarkStaleOfflineAsync`, `ExecuteUpdateAsync` em lote —
  não suportado pelo provider InMemory do EF Core, então só é coberto por
  teste manual/Postgres real) e no cache em memória
  (`ILiveMonitoringService.BroadcastExpiredDevices`), usando o mesmo TTL de
  90s (`LiveMonitoringService.PresenceTtl`) usado pelo cache ao vivo.

## Regras de negócio

- Toda operação é escopada por `OrganizationId` extraído do claim `orgId` do
  JWT — impossível ler/editar/apagar dispositivo de outra organização mesmo
  sabendo o `Guid`.
- Se o claim `orgId` estiver ausente/inválido, `Create` retorna `403 Forbid`
  explicitamente; as demais rotas (`GetAll`/`GetById`/`Update`/`Delete`)
  simplesmente usam `Guid.Empty` como organização, o que na prática devolve
  listas vazias/404 em vez de erro — comportamento inconsistente entre as
  rotas ao lidar com claim ausente.
- **Bug real no `Update`**: o controller só copia `Hostname` e `IpAddress` do
  `DeviceUpdateDTO` para a entidade (`existing.Hostname = dto.Hostname;
  existing.IpAddress = dto.IpAddress;`) — os campos `AgentEmail`, `Department`
  e `IsOnline` do DTO são recebidos mas **descartados**, nunca aplicados.
  Quem chamar `PUT /api/Devices/{id}` esperando atualizar o departamento, por
  exemplo, não verá efeito nenhum.
- Outros módulos (`AppFocusService.RegisterEventAsync`) fazem upsert
  automático de `Device` quando um evento chega de um dispositivo ainda não
  cadastrado — ou seja, o cadastro explícito via `POST /api/Devices` é
  opcional; o primeiro evento de telemetria de uma máquina nova já cria o
  registro (com `Hostname = "Desktop Agent"` como placeholder) e marca
  `IsOnline = true`.

## Correção manual do usuário

Sem correção manual, "quem está sendo monitorado numa máquina" é sempre
inferido (do `MonitoredUserId` que o agent manda em cada screenshot) — se o
agent foi instalado/configurado com a pessoa errada, o painel mostra o nome
errado até alguém reinstalar/reconfigurar o agent naquela máquina.

`PUT /api/Devices/{id}` aceita um `UserId` (`Guid?`) opcional em
`DeviceUpdateDTO` para um admin corrigir isso manualmente:

- O `UserId` enviado precisa pertencer à mesma organização do device — validado
  via `IUserService.GetUserByIdAsync(userId, orgId)` antes de aplicar; usuário
  de outra org retorna `400`.
- `Device.UserId` (quando setado) tem prioridade sobre o `MonitoredUserId`
  detectado pelo agent: `ScreenshotsController.Upload` calcula
  `effectiveUserId = device.UserId ?? dto.MonitoredUserId` antes de resolver
  quem aparece no card "ao vivo".
- A correção atualiza o cache em memória na hora, via
  `ILiveMonitoringService.UpdateDeviceUser(deviceId, userId, username)` —
  sem isso o painel só refletiria a mudança no próximo screenshot que chegasse
  daquela máquina.
- `MonitoringMessageDto.UserId`/`LiveDeviceUpdateDTO.UserId` propagam essa
  identidade pelo cache ao vivo; `RegisterOrUpdateDevice` preserva o `UserId`
  já em cache quando a chamada atual (ex.: heartbeat) não resolve nenhum, para
  não apagar a identidade conhecida com um valor vazio.

## Quem consome isso no desktop

O agente registra/atualiza seu próprio dispositivo implicitamente através dos
uploads de telemetria (screenshot, app-focus, etc.), que sempre incluem o
`DeviceId` gerado localmente (normalmente derivado do hostname da máquina).
Não há, hoje, um fluxo explícito no `wisemonitor_desktop` que chame
`POST /api/Devices` diretamente antes de começar a monitorar — o registro
acontece "de carona" no primeiro evento enviado.
