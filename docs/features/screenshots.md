# Capturas de tela (Screenshots)

Armazena screenshots periódicos enviados pelo agente desktop — a forma mais
simples e mais antiga de monitoramento visual do sistema (anterior à gravação
de vídeo real; ver `docs/video-streaming-flow.md` para o porquê de vídeo ter
sido adicionado depois, em paralelo, sem substituir isso).

Arquivos: `Controllers/ScreenshotsController.cs`, `Services/ScreenshotService.cs`,
`Repositories/ScreenshotRepository.cs`, `Models/Screenshot.cs`,
`DTOs/LiveMonitoringScreenshotUploadDTO.cs`,
`DTOs/LiveMonitoringScreenshotResponseDTO.cs`, `DTOs/LastScreenshotDTO.cs`.

## Modelo de dados

`Screenshot`: `OrganizationId`, `MonitoredUserId`, `DeviceId` (`string?`, ao
contrário de `AppFocusEvent.DeviceId` que é `Guid` — ver nota em
`docs/features/devices.md`), `CapturedAt`, `ImageData` (`byte[]`, a imagem
inteira em `bytea` no Postgres — mesmo padrão adotado depois por
`VideoSegment.VideoData`), `ContentType`, `SizeInBytes`, `ThumbnailData`
(campo existe mas nunca é preenchido em nenhum lugar do código — reservado
para uso futuro), `RowVersion` (concorrência otimista do EF Core, não
explorada em nenhuma lógica hoje), `FileName`.

## Endpoints

Base: `api/Screenshots`. Sem `[Authorize]` a nível de controller — cada ação
decide individualmente.

| Método | Rota | Auth | Uso |
|---|---|---|---|
| `POST` | `/upload` | Nenhuma (mas usa o token se presente) | Agente desktop envia uma screenshot (`multipart/form-data`, `LiveMonitoringScreenshotUploadDTO`), limite de 5MB, extensões `.png`/`.jpg`/`.jpeg` |
| `GET` | `/list` | `[Authorize]` | Lista a screenshot mais recente **de cada dispositivo** da organização (não é uma lista de todas — ver regra abaixo) |
| `GET` | `/last/{userId}` | Nenhuma | Última screenshot de um usuário monitorado, como base64 inline (`LastScreenshotDTO`) |
| `GET` | `/{id}` | `[AllowAnonymous]` explícito | Baixa a imagem (`image/png` ou o `ContentType` salvo) — sem auth de propósito, pois uma tag `<img>` do navegador não manda header `Authorization` |

## Regras de negócio

- **Retenção "mantém as últimas 10 por dispositivo"** (`ScreenshotRepository.
  UpsertAsync`): a cada upload, insere a nova e depois apaga qualquer
  screenshot além das 10 mais recentes para o mesmo `(MonitoredUserId,
  DeviceId)`. Insere antes de apagar deliberadamente, para não haver uma
  janela sem nenhuma imagem disponível caso alguém esteja servindo a URL
  antiga no exato momento da limpeza. Este é o mesmo padrão que
  `VideoSegmentRepository` usa, mas baseado em contagem, não em tempo.
- **`GET /list` filtra em memória**, não no banco: busca todas as screenshots
  da organização ordenadas por `CapturedAt` desc, depois usa um `HashSet` para
  manter apenas a primeira ocorrência de cada `DeviceId` (que, por já vir
  ordenado, é a mais recente). Funciona, mas escala mal se a organização tiver
  muitos dispositivos — toda a tabela é lida antes do filtro.
- **Upload sem autenticação obrigatória**: se `User.Identity.IsAuthenticated`
  for verdadeiro, o `OrganizationId` do DTO é sobrescrito pelo claim do token;
  caso contrário, o backend confia inteiramente no `OrganizationId`/
  `MonitoredUserId`/`DeviceId` enviados no corpo do multipart — ou seja, o
  agente desktop hoje consegue subir screenshots **sem JWT**, desde que saiba
  os IDs corretos. Isso é consistente com o padrão usado por
  `VideoSegmentsController`/`AppFocusController` (agente não necessariamente
  loga como "usuário" para enviar telemetria), mas vale ter em mente como
  superfície de ataque: qualquer requisição que adivinhe/vaze esses GUIDs
  pode inserir screenshots falsas numa organização.
- Após salvar, `ScreenshotsController.Upload` chama
  `ILiveMonitoringService.RegisterOrUpdateDevice(...)` para atualizar o estado
  "ao vivo" do dispositivo no dashboard (thumbnail/fullscreen URL apontando
  para a screenshot recém-salva) — é o mesmo mecanismo de notificação usado
  hoje por vídeo (`NotifyNewSegmentAsync`), mas para screenshot é uma
  atualização de estado (`RegisterOrUpdateDevice`), não um evento de
  broadcast — ver `docs/features/live-monitoring.md`.
- `ScreenshotCreateDTO` existe como arquivo mas está vazio (classe sem
  propriedades) — não é usado pelo `ScreenshotsController` atual, resquício
  de uma versão anterior do endpoint de upload.

## Quem envia isso no desktop

`ScreenshotService` (via `Utils/ScreenCaptureHelper`, compartilhado com a
gravação de vídeo) captura a tela em intervalo configurável
(`AgentConfig.ScreenshotIntervalSeconds`) e sobe via `multipart/form-data`
para `POST /api/Screenshots/upload`.
