# Sinalização WebRTC (`/ws/live`) — mecanismo separado e hoje não utilizado pelo vídeo real

**Resumo direto**: este módulo (`LiveController`, `LiveStreamHub`,
`DeviceWebSocketMiddleware`) implementa **apenas a sinalização** de uma
possível conexão WebRTC (troca de `offer`/`answer`/ICE candidates entre um
"producer" e um "viewer") — **nenhum byte de vídeo/áudio passa pelo
backend aqui**. A funcionalidade real de vídeo do sistema (gravação em
segmentos MP4, upload, reprodução com histórico) é outra coisa inteiramente
diferente, documentada em [`docs/video-streaming-flow.md`](../video-streaming-flow.md),
e **não usa nada deste módulo**. Isso foi confirmado nesta sessão de
desenvolvimento: não existe SFU, servidor de mídia, nem qualquer código que
receba/repasse frames de vídeo nesta rota — só mensagens de texto JSON de
sinalização.

Presume-se que este mecanismo tenha sido construído como uma tentativa
anterior de "vídeo ao vivo de baixa latência", mas para funcionar de verdade
precisaria de um cliente WebRTC real do lado do desktop e do dashboard (que
não foi encontrado no código deste repositório) e, em produção, de
STUN/TURN para NAT traversal — nada disso existe hoje. Trate este módulo como
**infraestrutura de sinalização pronta, mas órfã** — mantida por não
atrapalhar (não conflita com o vídeo por segmentos, que roda 100% sobre HTTP
simples), mas não é o caminho a estender se o objetivo for "vídeo ao vivo
melhor". Se o objetivo é reduzir a latência de ~10-20s do vídeo por
segmentos no futuro, uma implementação de WebRTC de verdade partiria daqui,
mas exigiria um SFU/relay de mídia que não existe ainda.

## 1. `LiveStreamHub` — salas em memória

`Services/LiveStreamHub.cs` mantém salas (`_rooms`) chaveadas por
`"{OrganizationId}:{SessionId}"`, cada uma com um dicionário de clientes
conectados (`LiveClient`, um `record` com `Socket`, `Role` (`Producer`/`Viewer`),
`OrganizationId`, `SessionId`, `UserId`, `LastPing`). Tudo em memória —
nenhuma tabela no banco, se a API reiniciar as salas somem.

`BroadcastControlAsync` repassa uma mensagem de sinalização para os outros
clientes da mesma sala, filtrando por tipo/papel via `ShouldForward`:

| Tipo da mensagem | Encaminhada para |
|---|---|
| `offer` | Viewers |
| `answer` | Producers |
| `candidate` / `ice-candidate` | Todos (exceto o remetente) |
| `viewer-join` / `viewer-left` | Producers |
| qualquer outro tipo | Descartada (`ShouldForward` retorna `false`) |

Esse é o padrão clássico de sinalização WebRTC: o servidor só participa da
negociação inicial (quem quer se conectar a quem, trocando SDP/ICE); depois
que a conexão P2P é estabelecida, mídia fluiria diretamente entre os pares
— **se** houvesse um cliente real dos dois lados implementando WebRTC, o
que não é o caso aqui.

## 2. `DeviceWebSocketMiddleware` — `/ws/live`

Registrado em `Program.cs` (`app.UseMiddleware<DeviceWebSocketMiddleware>()`,
antes do `TenantMiddleware`), intercepta apenas requisições WebSocket para
`/ws/live`. Diferente de `MonitorWebSocketMiddleware` (que lê o token da
query string), este lê as claims diretamente de `context.User` — ou seja,
espera que a autenticação JWT padrão do pipeline HTTP já tenha rodado antes
(ver ordem de middlewares na seção 5 de `docs/features/live-monitoring.md`).
Tenta várias variações de nome de claim para `organizationId` (schemas
antigos do WCF/Identity incluídos) — sinal de que já houve dor de cabeça
real com o claim não sendo encontrado no passado.

Fluxo por conexão:
1. Resolve `organizationId` das claims (`401` se ausente/inválido).
2. Obtém/cria uma `sessionId` via `ILiveSessionService.GetOrCreateSessionForOrganizationAsync`
   — **uma sessão por organização inteira**, não por usuário/dispositivo
   (ver `LiveSessionService`, seção 3) — o que significa que todos os
   producers/viewers da mesma org caem na mesma sala de sinalização.
3. Aceita o socket, espera a primeira mensagem ser `{"type":"hello","role":"producer"|"viewer",...}`
   para registrar o cliente no hub (`LiveStreamHub.AddClient`) com o papel
   declarado. Mensagens antes do `hello` são ignoradas.
4. Repassa `ping` para atualizar `LastPing` (sem lógica de expiração/limpeza
   automática de clientes com ping antigo visível no código — outra
   indicação de recurso incompleto).
5. Qualquer outra mensagem é tratada como sinal WebRTC e repassada via
   `BroadcastControlAsync`.

## 3. `LiveSessionService` — sessão por organização, em memória estática

`Services/LiveSessionService.cs` usa `ConcurrentDictionary` **estáticos**
(`static readonly`, compartilhados entre todas as instâncias do serviço no
processo) para mapear `organizationId -> sessionId` e
`(organizationId, userId) -> sessionId`. Métodos:
`GetOrCreateSessionForOrganizationAsync`, `GetOrCreateSessionForUserAsync`,
`EndSessionForOrganizationAsync`, `EndSessionForUserAsync`,
`HasActiveSession` (duas sobrecargas). Note que só o caminho "por
organização" é de fato usado pelo `DeviceWebSocketMiddleware` hoje — o
caminho "por usuário" existe na interface mas não tem nenhum chamador
localizado no código atual.

## 4. `LiveController` — endpoints HTTP auxiliares

Base: `api/live`.

| Método | Rota | Auth | Uso |
|---|---|---|---|
| `GET` | `/sessions/my-org` | `[Authorize]` | Snapshot da(s) sala(s) WebRTC ativa(s) da organização do usuário logado — quantos producers/viewers, quem está conectado |
| `GET` | `/health` | `[AllowAnonymous]` | Health check simples do serviço de sinalização (`{status:"ok", service:"LiveStream", timestamp}`) |

## 5. Recomendação

Se não há planos de implementar um cliente WebRTC real (desktop + dashboard)
no curto prazo, considerar este módulo como candidato a remoção/consolidação
— ele não é usado pelo fluxo de vídeo real e adiciona superfície de
manutenção (dois middlewares WebSocket, dois modelos de sessão) sem
benefício atual. Isso está fora do escopo desta documentação (que só
descreve o que existe), mas é um ponto que vale revisar com o time.
