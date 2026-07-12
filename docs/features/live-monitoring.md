# Monitoramento ao vivo — estado de dispositivos (dashboard)

Este documento cobre o `LiveMonitoringController`/`LiveMonitoringService` **na
parte que ainda está ativa hoje**: o registro de estado "este dispositivo está
online, é deste usuário, deste departamento" e sua distribuição ao dashboard
via WebSocket/SSE/polling. A gravação/reprodução de vídeo real (segmentos de
~10s) é uma funcionalidade separada, documentada em
[`docs/video-streaming-flow.md`](../video-streaming-flow.md) — este doc só
cobre o "board de dispositivos", não o vídeo em si.

## 1. O que existe hoje vs. o que existia antes

Antes da funcionalidade de vídeo por segmentos, "ao vivo" era simulado: o
desktop enviava uma foto JPEG por segundo via `POST /api/LiveMonitoring/screenshots`
(ainda existe, ver seção 4) e um endpoint `UploadFrame`/`IsWatched` que foi
**removido** nesta rodada de mudanças — o dashboard perguntava "alguém está
olhando este device?" e, se sim, recebia frames soltos, sem persistência.
Hoje esse caminho de frame-a-frame foi aposentado; o que resta em
`LiveMonitoringService` é o **bookkeeping de estado dos dispositivos** (quem
está online, thumbnail mais recente) e o canal WebSocket `/ws/monitor` que
também é reaproveitado por `VideoSegmentService.NotifyNewSegmentAsync` para
avisar sobre novos segmentos de vídeo prontos.

## 2. Modelo de dados (em memória, não persistido)

`LiveMonitoringService` guarda tudo em coleções `ConcurrentDictionary`
**em memória do processo** — não há tabela no Postgres para "estado ao vivo"
(o `Repositories/LiveMonitoringRepository.cs` existe como arquivo mas está
**vazio**, uma classe sem membros — resquício de um plano que não foi
concluído). Isso significa: se a API reiniciar, todo o estado "quem está
online agora" se perde até o próximo `update` de cada dispositivo.

- `_liveDevices: ConcurrentDictionary<string, MonitoringMessageDto>` — último
  estado conhecido de cada `deviceId`.
- `_messageCache: ConcurrentDictionary<string, List<MonitoringMessageDto>>` —
  histórico curto (até 50 mensagens) por `orgId`, cresce e descarta a mais
  antiga (`RemoveAt(0)`) quando excede.
- `_adminSockets: ConcurrentDictionary<string, ConcurrentDictionary<string, WebSocket>>` —
  sockets de dashboards conectados a `/ws/monitor`, agrupados por `orgId`.
- `_watchers: ConcurrentDictionary<string, ConcurrentDictionary<string, byte>>` —
  quais sessões de admin estão "assistindo" (`watch`/`unwatch`) cada
  `deviceId` agora. Hoje isso não altera nenhum comportamento de gravação
  (a gravação de vídeo roda independente de haver watcher — ver
  `docs/video-streaming-flow.md` seção 2) — é bookkeeping que sobrou do
  modelo antigo e não é consumido por mais nada visível no código atual.

`MonitoringMessageDto` (`DTOs/MonitoringMessageDTO.cs`): `OrgId`, `UserId`,
`DeviceId`, `Hostname`, `Ip`, `Username`, `Department`, `ThumbnailUrl?`,
`FullScreenUrl?`, `Status` ("online"/"offline"), `Type` (ex.: "screenshot",
"activity", "appFocus"), `Payload` (string livre — JSON/base64/texto),
`Timestamp`.

## 3. Referência de endpoints

Base: `api/LiveMonitoring`. Nenhuma ação tem `[Authorize]` explícito no
controller — a autenticação de quem *lê* o estado acontece só no nível do
WebSocket (`MonitorWebSocketMiddleware`, seção 5), não nestas rotas HTTP.
Isso é uma diferença notável de outros módulos do sistema, que protegem
endpoints individualmente.

| Método | Rota | Uso |
|---|---|---|
| `POST` | `/screenshots` | Upload de screenshot avulso (multipart, limite 5MB, delega para `IScreenshotService.SaveScreenshotAsync`) — funcionalidade de screenshot "clássico", ver `docs/features/screenshots.md` |
| `POST` | `/update` | Desktop registra/atualiza o estado do seu dispositivo (`LiveDeviceUpdateDTO`) |
| `GET` | `/devices` | Lista o estado atual de todos os dispositivos conhecidos (em memória) |
| `GET` | `/devices/{deviceId}` | Estado de um dispositivo específico, `404` se nunca reportou |
| `GET` | `/sse` | Server-Sent Events: reenvia a lista completa de devices a cada 2s enquanto a conexão ficar aberta |
| `GET` | `/polling` | Alternativa simples ao SSE/WebSocket — retorna o snapshot atual + timestamp numa única resposta |

## 4. `RegisterOrUpdateDevice` vs. `UpdateDevice`

Existem dois caminhos que escrevem em `_liveDevices`, com formatos
ligeiramente diferentes:

- `UpdateDevice(...)` (método com vários parâmetros posicionais) é chamado
  internamente por outros serviços (ex.: ao salvar um screenshot) para
  atualizar o estado com um `type`/`payload` específico.
- `RegisterOrUpdateDevice(LiveDeviceUpdateDTO dto)` é o que o endpoint
  `POST /update` chama diretamente — usa os campos do DTO tal como vieram do
  desktop, sem os parâmetros extras `type`/`payload`.

Ambos terminam chamando `BroadcastFrameAsync`, que reenvia o **estado
completo de todos os devices da org** (não só o que mudou) para os
dashboards conectados — simplifica o lado do cliente (sempre recebe a lista
inteira, consistente), ao custo de reenviar mais dados do que o
estritamente necessário a cada atualização.

## 5. Autenticação e roteamento do WebSocket `/ws/monitor`

`Middlewares/MonitorWebSocketMiddleware.cs` intercepta requisições
`/ws/monitor` **antes** dos outros middlewares de auth padrão do ASP.NET
(está registrado com `app.UseMiddleware<MonitorWebSocketMiddleware>()` em
`Program.cs`, junto com `DeviceWebSocketMiddleware` e antes do
`TenantMiddleware`). Ele:

1. Lê o JWT da query string (`?token=`) ou do header `Authorization: Bearer`.
2. Valida o token via `JwtHelper.ValidateToken` e extrai `organizationId` das
   claims.
3. Aceita o socket e chama `ILiveMonitoringService.RegisterAdmin(orgId, sessionId, socket)`.
4. Envia o estado atual imediatamente (`{ eventType: "update", payload: [...] }`).
5. Fica em loop lendo mensagens de texto do cliente — só entende dois
   comandos: `{"type":"watch","deviceId":"..."}` e `{"type":"unwatch",...}`
   (chamam `AddWatcher`/`RemoveWatcher`); qualquer outra coisa que não seja
   JSON válido é silenciosamente ignorada (tratado como ping/pong).
6. No `finally`, sempre desregistra o admin e remove os watchers dessa
   sessão, mesmo em caso de erro.

Este é o mesmo canal reaproveitado por `VideoSegmentService` para notificar
`{ eventType: "segment", ... }` quando um novo segmento de vídeo fica pronto
— um dashboard conectado recebe ambos os tipos de evento (`update` e
`segment`) na mesma conexão.

## 6. Relação com outros módulos

- **Vídeo (segmentos)**: `docs/video-streaming-flow.md` — usa o mesmo
  `/ws/monitor` para notificação, mas tem seu próprio armazenamento
  persistente (Postgres) e endpoints (`api/video-segments/*`).
- **Screenshots**: `POST /screenshots` neste controller delega para
  `IScreenshotService`, que é o mesmo serviço documentado em
  `docs/features/screenshots.md` — não há duplicação de lógica de
  persistência, só de rota de entrada.
- **Live (WebRTC signaling)**: `docs/features/live-webrtc-signaling.md` —
  mecanismo **totalmente separado** (`/ws/live`, `LiveStreamHub`), não
  compartilha estado com `LiveMonitoringService`.
