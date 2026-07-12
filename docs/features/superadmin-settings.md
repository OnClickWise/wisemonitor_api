# SuperAdmin — Configurações Globais da Plataforma

Um único registro (`PlatformSettings`, `Id = 1` fixo — não é uma tabela
multi-linha, é efetivamente uma linha de configuração singleton) com
parâmetros globais que afetam a plataforma inteira, fora do escopo de
qualquer tenant específico.

## 1. Endpoints (`SuperAdminSettingsController`, base `api/super-admin/settings`)

Nenhuma ação tem `[HasPermission(...)]` — só `[SuperAdminOnly]` herdado (ver
`docs/features/authorization-permissions.md` seção 4).

| Método | Rota | Uso |
|---|---|---|
| `GET` | `/` | Retorna as configurações atuais (ou um objeto com valores padrão do DTO se a linha ainda não existir no banco) |
| `PATCH` | `/` | Substitui **todos** os campos pelo payload enviado (ver seção 3 — não é um patch parcial de verdade) |

## 2. Campos (`PlatformSettingsDTO`/`Models/PlatformSettings.cs`)

Agrupados por área:

- **Registro**: `AllowPublicRegistration`, `RequireEmailVerification`,
  `DefaultPlanForNewTenants`, `TrialDurationDays`.
- **Segurança**: `EnforceIpAllowlistForSuperAdmin` + `AllowedIps` (array,
  serializado como JSON em `AllowedIpsJson`), `SessionTimeoutMinutes`,
  `MaxLoginAttempts`, `LockoutDurationMinutes`, `RequireMfaForSuperAdmin`.
- **Monitoramento**: `GlobalMaxScreenshotRetentionDays`,
  `ScreenshotCompressionQuality`.
- **Modo de manutenção**: `MaintenanceModeEnabled`, `MaintenanceModeMessage`,
  `MaintenanceScheduledAt`.
- **Notificações internas**: `NotifyOnNewTenantSignup`,
  `NotifyOnPaymentFailure`, `NotifyOnCriticalErrors`, `NotificationEmails`
  (array serializado em `NotificationEmailsJson`).

## 3. Nenhum destes campos é lido em outro lugar do código

Este é o ponto central a entender sobre este módulo: **é um formulário de
configuração persistido, mas nenhum destes valores é efetivamente
consultado/aplicado por outro serviço da API** nesta revisão do código.
Por exemplo:

- `EnforceIpAllowlistForSuperAdmin`/`AllowedIps` sugerem um allowlist de IP
  para acesso SuperAdmin, mas `SuperAdminAuthorizationFilter`
  (`docs/features/authorization-permissions.md`) não consulta
  `PlatformSettings` — só checa role/claim.
- `MaxLoginAttempts`/`LockoutDurationMinutes` sugerem um mecanismo de
  bloqueio por tentativas de login, mas não foi localizado no fluxo de
  `AuthController`/`AuthService` nenhuma leitura destes campos.
- `MaintenanceModeEnabled` sugere um modo de manutenção que bloquearia
  requisições, mas nenhum middleware consulta este valor.
- `GlobalMaxScreenshotRetentionDays` sugere um teto global de retenção, mas
  `ScreenshotRepository` (ver `docs/features/screenshots.md`) usa sua
  própria regra fixa "mantém os últimos 10 por device", sem relação com este
  campo.

Trate esta tela hoje como **um formulário de intenção/roadmap** persistido
corretamente no banco, mas ainda não conectado ao comportamento real do
sistema — cada campo é um candidato a virar uma feature de verdade, não uma
feature já ativa.

## 4. `PATCH /` substitui o objeto inteiro

`UpdateAsync` não faz merge parcial campo a campo (diferente de outros
módulos do sistema que só atualizam campos não-nulos do DTO) — ele copia
**todos** os campos do `PlatformSettingsDTO` recebido para a entidade,
inclusive os que não foram intencionalmente alterados pelo cliente. Um
cliente que monte o payload de update a partir de um `GET` anterior está
seguro; um cliente que envie um DTO parcialmente populado vai sobrescrever
os demais campos com os valores padrão do DTO (`false`/`0`/strings vazias).
Cria a linha (`Id = 1`) se ainda não existir. Grava `LastUpdatedByUserId` e
`UpdatedAt` a cada chamada.
