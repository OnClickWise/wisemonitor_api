# Documentação — WiseMonitor API

Índice de toda a documentação funcional do backend. Cada arquivo em
`features/` cobre um módulo por completo: o que faz, modelo de dados,
referência de endpoints, regras de negócio reais (não idealizadas) e como se
relaciona com os demais módulos.

## Identidade & Organização

- [`auth.md`](features/auth.md) — login, logout, recuperação/redefinição de senha, JWT
- [`register-organization.md`](features/register-organization.md) — registro de nova organização (tenant) + admin inicial
- [`users.md`](features/users.md) — CRUD de usuários, papéis, vínculo com organização
- [`tenant-branding.md`](features/tenant-branding.md) — white-label (logo, cores) por tenant
- [`teams.md`](features/teams.md) — equipes, gestores e membros
- [`departments.md`](features/departments.md) — departamentos e seus membros
- [`delegations.md`](features/delegations.md) — delegação temporária de permissões/gestão entre usuários

## Jornada, dispositivos e dados de monitoramento

- [`work-schedules.md`](features/work-schedules.md) — jornadas de trabalho, regras por dia, tolerância
- [`devices.md`](features/devices.md) — cadastro e status de dispositivos monitorados
- [`app-focus.md`](features/app-focus.md) — foco de aplicação/janela/URL e classificação de produtividade
- [`screenshots.md`](features/screenshots.md) — capturas de tela periódicas, retenção "últimas 10"
- [`keyboard-monitoring.md`](features/keyboard-monitoring.md) — estatísticas de digitação (sem armazenar teclas reais)
- [`audit-logs.md`](features/audit-logs.md) — trilha de auditoria por organização

## Ao vivo, vídeo e distribuição do agente

- [`live-monitoring.md`](features/live-monitoring.md) — estado dos dispositivos em tempo real (dashboard), SSE/polling
- [`../video-streaming-flow.md`](video-streaming-flow.md) — **vídeo real em segmentos (~10s)**: gravação, upload, histórico correlacionado — a funcionalidade "ao vivo" de fato
- [`live-webrtc-signaling.md`](features/live-webrtc-signaling.md) — sinalização WebRTC (`/ws/live`), mecanismo separado e hoje não usado pelo vídeo real
- [`agent-distribution.md`](features/agent-distribution.md) — download/versionamento do instalador do agente desktop

## Autorização

- [`authorization-permissions.md`](features/authorization-permissions.md) — sistema de permissões (`HasPermission`, `RolePermissionMatrix`) usado por todos os módulos acima

## SuperAdmin (administração da plataforma, cross-tenant)

- [`superadmin-tenants.md`](features/superadmin-tenants.md) — gestão de tenants
- [`superadmin-users.md`](features/superadmin-users.md) — gestão de usuários cross-tenant
- [`superadmin-metrics.md`](features/superadmin-metrics.md) — métricas da plataforma
- [`superadmin-alerts.md`](features/superadmin-alerts.md) — regras de alerta e histórico
- [`superadmin-agent-management.md`](features/superadmin-agent-management.md) — gestão de versões do agente desktop
- [`superadmin-integrations.md`](features/superadmin-integrations.md) — integrações da plataforma
- [`superadmin-settings.md`](features/superadmin-settings.md) — configurações globais
- [`superadmin-audit.md`](features/superadmin-audit.md) — auditoria da plataforma (cross-tenant)

---

Documentação do agente desktop (o outro lado do sistema): ver
[`docs/`](https://github.com/OnClickWise/wisemonitor_desktop/tree/main/docs)
no repositório `wisemonitor_desktop`.
