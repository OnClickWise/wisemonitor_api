# WiseMonitor API

## Visão Geral

O **WiseMonitor API** é uma API REST desenvolvida em **ASP.NET Core** com **Entity Framework Core**, projetada como um **SaaS multi-tenant** para monitoramento de trabalho remoto, gestão de usuários, jornadas de trabalho, dispositivos e atividades em tempo real.

A arquitetura segue boas práticas de **Clean Architecture**, separando responsabilidades em Controllers, Services, Repositories, DTOs, Models, Middlewares e Utils.

---

## Tecnologias Utilizadas

* ASP.NET Core Web API
* Entity Framework Core
* PostgreSQL
* JWT Authentication
* SignalR / SSE (monitoramento em tempo real)
* Swagger / OpenAPI
* Arquitetura Multi-Tenant

---

## Estrutura do Projeto

```
Configs/
Controllers/
Data/
DTOs/
Extensions/
Handlers/
Helpers/
Middlewares/
Models/
Repositories/
Services/
Utils/
```

---

```
🔄 Migrations e Banco de Dados

Sempre que houver alteração em Models ou Data:

dotnet ef migrations add NomeDaMigration
dotnet ef database update


📌 O sistema bloqueia execução se houver mudanças pendentes não migradas.

▶️ Como Executar o Projeto
dotnet restore
dotnet build
dotnet ef database update
dotnet run


A API ficará disponível em:

http://localhost:8080


Swagger:

http://localhost:8080/swagger
``

---

## Módulos da API

### 🔐 Autenticação (Auth)

Responsável por autenticação, controle de sessão e recuperação de senha.

**Endpoints:**

* POST `/api/Auth/login`
* POST `/api/Auth/logout`
* POST `/api/Auth/forgot-password`
* POST `/api/Auth/reset-password`

**Funcionalidades:**

* Login com JWT
* Logout
* Recuperação e redefinição de senha
* Autenticação vinculada à organização (multi-tenant)

---

### 🏢 Registro de Organização

Criação e consulta de organizações (empresas) no sistema.

**Endpoints:**

* POST `/api/RegisterOrganization`
* GET `/api/RegisterOrganization/me`

**Funcionalidades:**

* Registro de nova organização
* Criação automática do administrador
* Base do modelo multi-tenant

---

### 👤 Usuários (User)

Gerenciamento completo de usuários.

**Endpoints:**

* POST `/api/User`
* GET `/api/User`
* PUT `/api/User/{id}`
* DELETE `/api/User/{id}`

**Funcionalidades:**

* CRUD de usuários
* Vínculo com organização
* Controle de status (ativo/inativo)
* Definição de papéis

---

### 👥 Equipes (Teams)

Gestão de equipes e seus membros.

**Endpoints:**

* POST `/api/teams`
* GET `/api/teams`
* GET `/api/teams/{id}`
* PUT `/api/teams/{id}`
* DELETE `/api/teams/{id}`
* POST `/api/teams/{id}/members/{userId}`
* DELETE `/api/teams/{id}/members/{userId}`

**Funcionalidades:**

* Criação e edição de equipes
* Definição de gestores
* Associação e remoção de membros

---

### 💻 Dispositivos (Devices)

Controle dos dispositivos monitorados.

**Endpoints:**

* POST `/api/Devices`
* GET `/api/Devices`
* GET `/api/Devices/{id}`
* PUT `/api/Devices/{id}`
* DELETE `/api/Devices/{id}`

**Funcionalidades:**

* Registro de dispositivos
* Identificação por host/IP
* Status online/offline

---

### 🕒 Jornada de Trabalho (Work Schedules)

Define regras e horários de trabalho dos usuários.

**Endpoints:**

* POST `/api/work-schedules`
* GET `/api/work-schedules`
* GET `/api/work-schedules/{id}`
* PUT `/api/work-schedules/{id}`
* DELETE `/api/work-schedules/{id}`
* POST `/api/work-schedules/assign`

**Funcionalidades:**

* Criação de jornadas flexíveis ou fixas
* Regras por dia da semana
* Controle de tolerância e pausa
* Associação de jornada ao usuário

---

### 📊 Monitoramento de Aplicações (App Focus)

Rastreamento de aplicativos, janelas e URLs acessadas.

**Endpoints:**

* POST `/api/monitoring/app-focus`
* GET `/api/monitoring/app-focus`
* GET `/api/monitoring/app-focus/{id}`
* PUT `/api/monitoring/app-focus/{id}`
* DELETE `/api/monitoring/app-focus/{id}`
* GET `/api/monitoring/app-focus/user/{userId}`
* GET `/api/monitoring/app-focus/metrics`

**Funcionalidades:**

* Registro de foco de aplicação
* Análise de produtividade
* Métricas por período

---

### 📸 Capturas de Tela (Screenshots)

Armazenamento e consulta de screenshots do monitoramento.

**Endpoints:**

* POST `/api/Screenshots/upload`
* GET `/api/Screenshots/list`
* GET `/api/Screenshots/last/{userId}`
* GET `/api/Screenshots/{id}`

**Funcionalidades:**

* Upload multipart/form-data
* Consulta histórica
* Última captura por usuário

---

### 🔴 Monitoramento ao Vivo (Live Monitoring)

Estado dos dispositivos em tempo real (dashboard).

**Endpoints:**

* POST `/api/LiveMonitoring/screenshots`
* POST `/api/LiveMonitoring/update`
* GET `/api/LiveMonitoring/devices`
* GET `/api/LiveMonitoring/devices/{deviceId}`
* GET `/api/LiveMonitoring/sse`
* GET `/api/LiveMonitoring/polling`
* GET `/api/live/sessions/my-org`
* GET `/api/live/health`

**Funcionalidades:**

* Atualização de status dos devices
* SSE e Polling
* Monitoramento multi-dispositivo

---

### 🎥 Vídeo Ao Vivo + Histórico (Video Segments)

Vídeo real da tela (segmentos de ~10s), tanto para "ao vivo" quanto para
histórico correlacionado com app-focus/teclado. Ver
[`docs/video-streaming-flow.md`](docs/video-streaming-flow.md) para o fluxo
completo (desktop → backend → dashboard).

**Endpoints:**

* POST `/api/video-segments/upload`
* GET `/api/video-segments/{id}`
* GET `/api/video-segments/latest?deviceId=`
* GET `/api/video-segments/history?deviceId=&from=&to=`

**Funcionalidades:**

* Gravação contínua enquanto o monitoramento está ativo
* Reprodução com suporte a `Range` (`<video>` do navegador)
* Retenção configurável por tempo (`VIDEO_SEGMENT_RETENTION_HOURS`)
* Histórico correlacionado com app em foco e palavras digitadas

---

## Arquitetura e Padrões

* Clean Architecture
* Repository Pattern
* DTO Pattern
* Validações centralizadas
* Middlewares para segurança
* Separação clara de responsabilidades

---

## Segurança

* Autenticação JWT
* Multi-Tenant por OrganizationId
* Controle de acesso por perfil
* Validação de headers obrigatórios

---

## Roadmap

* Relatórios avançados
* Integração com BI
* Auditoria e logs
* SLA por organização
* Webhooks

---

## Licença

Projeto proprietário — OnClickWise ©
Author Leovigildo Miguel, Arthur Torres

