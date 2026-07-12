# SuperAdmin — Integrações da Plataforma

CRUD de integrações externas configuráveis a nível de plataforma (não por
tenant): SMTP, Slack, Webhook, Zapier, AWS S3, Sentry, Datadog, PagerDuty
(valores de exemplo documentados no comentário de `Models/PlatformIntegration.cs`,
campo `Type` é string livre, sem `enum` reforçado no banco).

## 1. Endpoints (`SuperAdminIntegrationsController`, base `api/super-admin/integrations`)

Nenhuma ação tem `[HasPermission(...)]` — só `[SuperAdminOnly]` herdado (ver
`docs/features/authorization-permissions.md` seção 4).

| Método | Rota | Uso |
|---|---|---|
| `GET` | `/` | Lista todas as integrações configuradas, ordenadas por nome |
| `POST` | `/` | Cria uma nova integração |
| `PATCH` | `/{id}` | Atualiza nome/config/eventos/ativo |
| `DELETE` | `/{id}` | Remove a integração |
| `POST` | `/{id}/test` | Testa "conectividade" — ver seção 3, é um teste bem raso |

## 2. Modelo (`PlatformIntegration`)

`Name`, `Type`, `ConfigJson` (JSON livre serializado como texto — o
comentário no modelo alerta explicitamente "nunca expor senhas em texto
plano", mas **não há criptografia aplicada a `ConfigJson` no código
atual** — é gravado no banco exatamente como veio do `Dictionary<string,string>`
do DTO, serializado em JSON puro. Se `ConfigJson` guardar credenciais reais
(ex.: senha SMTP, token de webhook), hoje elas ficam em texto plano no
Postgres — vale revisar antes de usar isso em produção com segredos reais),
`IsActive`, `EventsJson` (array de eventos que disparariam a integração, ex.
`["tenant.created","tenant.suspended","billing.failed","error.critical"]`
— mesma observação de "sem conexão real" que os `Trigger`s de
`docs/features/superadmin-alerts.md`: não há código encontrado que de fato
dispare uma integração quando um desses eventos ocorre), `LastTestedAt`,
`LastTestSuccess`, `LastTestMessage`.

## 3. "Teste de conectividade" não testa conectividade real

`TestConnectivityAsync` não abre nenhuma conexão de rede real com o serviço
externo (não envia e-mail de teste via SMTP, não faz `POST` num webhook,
etc.) — o "teste" só desserializa `ConfigJson` como
`Dictionary<string,string>` e verifica se o resultado não é nulo/vazio.
Ou seja, uma configuração de Slack com um `webhookUrl` completamente
inválido (URL inexistente, token errado) passaria no teste, desde que o
JSON esteja bem formado e tenha ao menos uma chave. Isso é adequado como
validação de "a config não está corrompida", mas não deve ser lido pelo
usuário final como "confirmei que a integração funciona".

## 4. Relação com outros módulos

- Ver `docs/features/superadmin-alerts.md` seção 4 — os canais de
  notificação de `AlertRule` (`email`/`slack`/`webhook`) presumivelmente se
  conectariam a integrações deste tipo num fluxo completo, mas hoje as duas
  tabelas não têm nenhuma referência cruzada no código.
