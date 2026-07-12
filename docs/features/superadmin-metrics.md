# SuperAdmin — Métricas da Plataforma

Painel de visão geral cross-tenant: quantos tenants/usuários/dispositivos
existem, série temporal de crescimento, distribuição por plano e saúde geral
do sistema. Todas as ações exigem `Permissions.MetricsGlobal` (só concedida
ao papel `SuperAdmin`, ver `docs/features/authorization-permissions.md`).

## 1. Endpoints (`SuperAdminMetricsController`, base `api/super-admin/metrics`)

| Método | Rota | Uso |
|---|---|---|
| `GET` | `/` | Visão geral agregada da plataforma inteira |
| `GET` | `/timeseries` | Série temporal de uma métrica específica, por período |
| `GET` | `/plans` | Distribuição percentual de tenants por plano |
| `GET` | `/api/super-admin/system/health` | Saúde do sistema (nota: rota absoluta, ver seção 4) |

## 2. `GetOverviewAsync` — números reais e placeholders

A visão geral (`PlatformOverviewDTO`) combina contagens reais do banco com
alguns campos que **hoje são sempre zero**, não calculados de fato:

- **Reais**: `TotalTenants`, `ActiveTenants`, `SuspendedTenants`,
  `TrialTenants`, `NewTenantsLast30d`, `ChurnLast30d` (organizações
  suspensas nos últimos 30 dias — proxy simples de churn, não considera
  reativação), `TotalUsers`, `ActiveUsersLast7d`/`Last30d` (nota: filtra por
  `u.IsActive && u.CreatedAt >= X` — ou seja, mede usuários **criados**
  recentemente que estão ativos, não usuários com atividade recente; o nome
  do campo pode induzir a leitura errada de "logaram nos últimos 7 dias"),
  `TotalDevices`, `OnlineDevicesNow`, `ScreenshotsTakenLast24h`.
- **Sempre zero (placeholders)**: `StorageUsedGbTotal`,
  `ApiAvgResponseTimeMs`, `ApiErrorRateLast1h`, `QueuePendingJobs` — não há
  nenhuma coleta de métricas de infraestrutura (APM, storage accounting)
  integrada; esses campos existem no DTO mas nenhum código popula valores
  reais. Se o dashboard exibir esses números, hoje sempre mostrará "0" sem
  isso significar literalmente zero uso.

## 3. Séries temporais suportadas

`GetTimeseriesAsync` reconhece apenas três valores de `Metric` via `if`
sequenciais (não um `switch`/mapa extensível): `"NewTenants"`,
`"ActiveUsers"`, `"Screenshots"` — qualquer outro valor retorna lista vazia
silenciosamente (sem erro `400`). Período (`Period`) aceita `"7d"`, `"90d"`,
`"1y"`, com `"30d"` como padrão para qualquer outro valor. O agrupamento é
feito **em memória** (`.ToListAsync()` traz todas as datas para o processo,
depois `.GroupBy(d => d)` em LINQ-to-Objects) — para tenants/screenshots com
volume muito alto isso pode carregar uma lista grande antes de agrupar; hoje
não é um problema visível mas é uma escolha a rever se o volume crescer
muito.

## 4. Rota "absoluta" de `system/health`

`GetSystemHealth` está anotada com
`[HttpGet("/api/super-admin/system/health")]` — a barra inicial faz o
ASP.NET Core tratar isso como uma rota **absoluta**, ignorando o prefixo
`api/super-admin/metrics` do controller. Ou seja, apesar de estar dentro de
`SuperAdminMetricsController`, o endpoint responde em
`/api/super-admin/system/health`, não em
`/api/super-admin/metrics/system/health`. Isso é válido e funciona, mas é
fácil de esquecer ao procurar esse endpoint pela convenção de rota do
controller.

`GetSystemHealthAsync` testa conectividade real com
`_context.Database.CanConnectAsync()` (com try/catch silencioso — falha vira
`dbOk = false`, não uma exceção não tratada) e reporta
`TotalMigrations` (contagem de migrations já aplicadas) e `UptimeHours`
calculado a partir de um campo estático `_startupTime` capturado na
inicialização do serviço (aproximação razoável de uptime do processo, não do
container/máquina).

## 5. Relação com outros módulos

- `PlanDistribution` usa o mesmo campo `Organization.Plan` gerenciável via
  `docs/features/superadmin-tenants.md` (`UpdatePlanAsync`).
- Números de `Screenshots`/`Devices` referenciam as mesmas tabelas
  documentadas em `docs/features/screenshots.md` e `docs/features/devices.md`.
