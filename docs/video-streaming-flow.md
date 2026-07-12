# Fluxo de vídeo ao vivo (Live) — WiseMonitor

Este documento explica como a tela do usuário monitorado chega, em vídeo real,
até o dashboard — tanto para visualização "ao vivo" quanto para revisão de
histórico. Cobre o agente desktop (`wisemonitor_desktop`) e a API
(`wisemonitor_api`); a UI que efetivamente exibe o vídeo fica no dashboard web
(repositório separado) e é apenas um consumidor desta API.

## 1. Por que segmentos de vídeo, e não WebRTC

Antes desta funcionalidade, o "ao vivo" era simulado: o desktop perguntava ao
backend a cada 3s "alguém está me assistindo?" e, se sim, enviava uma foto
JPEG por segundo. Nada disso era persistido — a imagem mais recente ficava só
em memória e era perdida assim que uma nova chegava. Não existia histórico.

Havia também um mecanismo de sinalização WebRTC (`/ws/live`, `LiveStreamHub`)
já pronto no backend, mas ele **nunca transportou vídeo de verdade** — só
repassa mensagens `offer`/`answer`/`ICE candidate` entre dois lados que
precisariam negociar uma conexão P2P sozinhos. Implementar isso de verdade
exigiria um servidor de mídia (SFU) ou NAT traversal (STUN/TURN) — infra nova,
mais complexa e mais arriscada.

A abordagem adotada é **gravação em segmentos curtos (~10s) de vídeo MP4 real**,
no estilo HLS: o desktop grava um pedaço, sobe pro backend, e assim que
termina começa o próximo. Isso:

- Usa só HTTP simples (o mesmo transporte usado em todo o resto do app) — sem
  STUN/TURN/SFU.
- Os próprios segmentos **são** o histórico — não existe uma trilha separada
  para "ao vivo" vs "revisão".
- Tem uma latência de ~10-20s (o segmento precisa terminar de gravar e subir
  antes de poder ser assistido) — inaceitável para uma chamada de vídeo, mas
  perfeitamente aceitável para monitoramento de produtividade.

## 2. Visão geral do fluxo

```
┌─────────────────────────┐         ┌──────────────────────────────────────┐
│   Desktop (agente)       │         │   Backend (wisemonitor_api)           │
│                          │         │                                        │
│  VideoSegmentRecording   │  POST   │  VideoSegmentsController              │
│  Service                 │ ──────► │   → VideoSegmentService               │
│   1. captura tela        │ upload  │      → VideoSegmentRepository         │
│      (~2 fps)            │         │        (Postgres, bytea, tenant-      │
│   2. codifica c/ ffmpeg  │         │         isolado, retenção por tempo)   │
│      (H.264 MP4, ~10s)   │         │      → LiveMonitoringService          │
│   3. envia; se falhar,   │         │        .NotifyNewSegmentAsync()       │
│      guarda localmente   │         │        (broadcast via /ws/monitor)    │
│      (SQLite) p/ retry   │         │                                        │
└─────────────────────────┘         └──────────────────┬─────────────────────┘
                                                          │
                                     GET /latest, /history, /{id}
                                                          │
                                                          ▼
                                          ┌───────────────────────────┐
                                          │  Dashboard (frontend)      │
                                          │  - "Ao vivo": pede o        │
                                          │    segmento mais recente   │
                                          │    e/ou escuta /ws/monitor │
                                          │  - Histórico: pede uma      │
                                          │    faixa de tempo + contexto│
                                          └───────────────────────────┘
```

## 3. Lado do Desktop

Arquivo principal: `Services/VideoSegmentRecordingService.cs`.

1. **Início**: chamado pelo `AgentOrchestrator.Start(...)` quando o toggle
   `CaptureVideoSegments` está ligado (ver `AgentConfig`/`ConfigView`). Roda em
   paralelo com screenshot, teclado, app-focus e métricas de sistema — **não**
   depende de ninguém estar assistindo ao vivo naquele momento, porque o
   histórico precisa existir de qualquer forma.
2. **Preparação do ffmpeg**: na primeira vez que roda, baixa o binário do
   ffmpeg (via `Xabe.FFmpeg.Downloader`) para
   `%LocalAppData%\WiseMonitor\ffmpeg\`. Isso evita ter que empacotar um
   executável de ~100MB no instalador — o download acontece uma vez por
   máquina.
3. **Captura de frames**: a cada `VideoSegmentFrameIntervalMs` (padrão 500ms →
   ~2 fps), tira uma foto da tela inteira (todos os monitores, via
   `Utils/ScreenCaptureHelper.CaptureAllScreens()` — a mesma rotina usada pelo
   `ScreenshotService`), reduz para no máximo 1280px de largura, e salva como
   JPEG numa pasta temporária (`%TEMP%\WiseMonitor\video-segments\{segmentId}\`).
   Isso continua por `VideoSegmentDurationSeconds` (padrão 10s).
4. **Codificação**: ao fechar o segmento, os JPEGs da pasta são passados pro
   ffmpeg (`-f image2 -framerate N -i frame_%04d.jpg -c:v libx264 -pix_fmt
   yuv420p`), gerando um `.mp4` H.264. Isso roda em paralelo com a captura do
   *próximo* segmento (não bloqueia).
   - **Importante**: largura e altura sempre são arredondadas para números
     pares — o codec H.264 com `yuv420p` (subsampling de croma 2x2) rejeita
     dimensões ímpares. Isso já causou uma falha real em produção com uma tela
     escalada para 1280x719.
5. **Upload**: `VideoSegmentApiService.UploadSegmentAsync(...)` sobe o MP4 via
   multipart para `POST /api/video-segments/upload`. Se der certo, o arquivo
   local é apagado. Se falhar (rede fora, etc.), o caminho do arquivo é
   guardado numa tabela SQLite local (`PendingVideoSegment`, em
   `Data/AppDbContext.cs`) e o `OfflineEventQueueService` tenta reenviar a cada
   30s, descartando depois de 20 tentativas sem sucesso.

### Configuração (agente)

| Variável (.env)                     | Campo em `AgentConfig`         | Padrão |
|--------------------------------------|----------------------------------|--------|
| `CAPTURE_VIDEO_SEGMENTS`             | `CaptureVideoSegments`           | `true` |
| `VIDEO_SEGMENT_DURATION_SECONDS`     | `VideoSegmentDurationSeconds`    | `10`   |
| `VIDEO_SEGMENT_FRAME_INTERVAL_MS`    | `VideoSegmentFrameIntervalMs`    | `500`  |

## 4. Lado do Backend

Arquivos principais: `Controllers/VideoSegmentsController.cs`,
`Services/VideoSegmentService.cs`, `Repositories/VideoSegmentRepository.cs`,
`Models/VideoSegment.cs`.

1. **Armazenamento**: cada segmento é uma linha na tabela `VideoSegments`
   (Postgres), com os bytes do MP4 direto num `bytea` — o mesmo padrão já usado
   para screenshots (`Screenshot.ImageData`). Isolamento de tenant é garantido
   por um EF Core global query filter (igual às outras ~14 entidades
   multi-tenant do sistema).
2. **Retenção**: a cada upload, o repositório apaga (para o mesmo
   `OrganizationId`+`DeviceId`) qualquer segmento cujo `EndedAt` seja mais
   antigo que a janela de retenção (`VideoSegmentRetentionHours`, padrão 4h —
   configurável via `appsettings.json` ou env var
   `VIDEO_SEGMENT_RETENTION_HOURS`). Não existe um job agendado separado —
   a poda acontece "de carona" em cada upload, no mesmo padrão que
   `ScreenshotRepository` já usa (lá é "mantém os últimos 10", aqui é
   "mantém os últimos N horas").
3. **Notificação em tempo real**: depois de salvar, `VideoSegmentService`
   chama `ILiveMonitoringService.NotifyNewSegmentAsync(...)`, que reaproveita
   o canal WebSocket `/ws/monitor` já usado pelo dashboard para receber
   atualizações de estado dos devices. A mensagem tem este formato:
   ```json
   { "eventType": "segment", "deviceId": "...", "segmentId": "...", "startedAt": "...", "endedAt": "..." }
   ```
   Um dashboard conectado a esse socket e "olhando" para aquele device pode
   reagir imediatamente (buscar o novo segmento) em vez de fazer polling.
4. **Correlação com contexto**: `GetHistoryWithContextAsync` busca, para a
   mesma janela de tempo de cada segmento, os `AppFocusEvent` (app em foco) e
   `KeyboardSession` (o que foi digitado, agregado por palavra) do mesmo
   usuário/organização cuja faixa `[StartTime,EndTime]`/`[StartAt,EndAt]`
   **sobrepõe** a faixa do segmento — não é "mesmo dia", é sobreposição real de
   intervalo, para não trazer contexto de horas antes/depois por engano.

## 5. Como o dashboard deve consumir isso

**Visualização "ao vivo"** (usuário clica num device na tela de Live):
1. Chamar `GET /api/video-segments/latest?deviceId=X` para pegar o segmento
   mais recente e tocar.
2. Continuar chamando esse mesmo endpoint (poll a cada alguns segundos) ou
   escutar o evento `segment` no `/ws/monitor` para saber quando um novo
   segmento está pronto, e então buscar e tocar o próximo — isso dá o efeito
   de "ao vivo" com uns 10-20s de atraso, encadeando segmento após segmento.
   (Não existe hoje um manifesto tipo HLS `.m3u8` — o encadeamento é
   responsabilidade do player no frontend.)

**Histórico** (usuário quer ver o que aconteceu num intervalo passado):
1. Chamar `GET /api/video-segments/history?deviceId=X&from=...&to=...`.
2. Cada item já vem com `context.appFocusEvents`/`context.keyboardSessions`
   correlacionados — dá pra mostrar "estava no Chrome, aba tal" e "digitou
   estas palavras" ao lado do vídeo daquele trecho, sem precisar de outra
   chamada.
3. Baixar/tocar cada vídeo via a `url` já incluída em cada segmento
   (`GET /api/video-segments/{id}`, que suporta `Range` — o player HTML5
   `<video>` já faz isso sozinho ao usar a URL como `src`).

## 6. Referência de endpoints

Base: `api/video-segments`. Todos exigem JWT (`Authorize`), exceto o download
por id (`AllowAnonymous`, mesmo racional do `ScreenshotsController` — a tag
`<video>`/`<img>` do navegador não manda header de autenticação).

| Método | Rota | Uso |
|---|---|---|
| `POST` | `/upload` | Desktop agent envia um segmento gravado (multipart: `Segment`, `DeviceId`, `OrganizationId`, `MonitoredUserId`, `StartedAt`, `EndedAt`) |
| `GET` | `/{id}` | Baixa/reproduz o vídeo (suporta `Range`, retorna `video/mp4`) |
| `GET` | `/latest?deviceId=` | Metadados do segmento mais recente de um device (para iniciar o "ao vivo") |
| `GET` | `/history?deviceId=&from=&to=` | Lista de segmentos no intervalo + contexto de app-focus/teclado correlacionado |

## 7. Limitações conhecidas / decisões em aberto

- **Latência ~10-20s**, não é vídeo em tempo real de baixa latência (decisão
  deliberada — ver seção 1).
- **Gravação contínua enquanto o monitoramento está ativo**, independente de
  alguém estar assistindo ao vivo — isso tem custo real de banda/armazenamento
  (mitigado por baixa resolução/fps e retenção curta), mas é necessário para o
  histórico existir.
- **Armazenamento em Postgres (`bytea`)**, não em object storage (GCS/S3).
  Mais simples e não exige infraestrutura nova, mas escala pior que um bucket
  dedicado — se o volume crescer muito, migrar `VideoSegmentRepository` para
  GCS é uma mudança contida (a API pública não muda).
- **Sem geração de thumbnail** por segmento — o dashboard precisaria gerar uma
  prévia do lado do cliente ou isso vira um item futuro no backend.
- **Sem transcodificação adaptativa** (qualidade única, fixa). Não é um
  requisito hoje, mas se o histórico for assistido em conexões ruins, vale
  reconsiderar.
