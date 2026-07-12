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
`UpdatedAt`, `OrganizationId` (multi-tenant).

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
| `PUT` | `/{id}` | Atualiza Hostname/IpAddress (não atualiza AgentEmail/Department apesar do DTO aceitar — ver bug abaixo) |
| `DELETE` | `/{id}` | Remove um dispositivo |

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

## Quem consome isso no desktop

O agente registra/atualiza seu próprio dispositivo implicitamente através dos
uploads de telemetria (screenshot, app-focus, etc.), que sempre incluem o
`DeviceId` gerado localmente (normalmente derivado do hostname da máquina).
Não há, hoje, um fluxo explícito no `wisemonitor_desktop` que chame
`POST /api/Devices` diretamente antes de começar a monitorar — o registro
acontece "de carona" no primeiro evento enviado.
