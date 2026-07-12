# Autenticação (Auth)

Login, logout e recuperação de senha. É a porta de entrada para todo o resto
da API — praticamente todo endpoint autenticado depende do JWT emitido aqui.

## 1. Por que existe

O sistema é multi-tenant (várias organizações no mesmo banco), então o login
precisa, além de validar credenciais, carimbar o token com a organização do
usuário (`orgId`) — é esse claim que todo o resto da API usa para isolar dados
entre tenants (`User.GetOrganizationId()`, usado em praticamente todo
controller).

## 2. Duas implementações de login coexistindo

Vale notar isso porque é uma inconsistência real do código, não uma escolha
deliberada: existem **dois** métodos de login em `AuthService`:

- `LoginAsync(LoginRequestDTO)` — exige `Email` + `Password` + `OrganizationId`
  já conhecido, gera o JWT manualmente dentro do próprio `AuthService`
  (`GenerateJwtToken`), lendo a config em `JwtSettings:SecretKey` via
  `IConfiguration` diretamente.
- `LoginByEmailAsync(email, password)` — só exige e-mail/senha (não precisa
  saber a organização de antemão, já que busca o usuário só pelo e-mail), e
  delega a emissão do token para `IJwtService.GenerateToken(user)`.

**Só o segundo é usado hoje** — é o que `AuthController.Login` chama. O
primeiro (`LoginAsync`) não é referenciado por nenhum controller ativo; é
código morto que ainda existe na interface `IAuthService` e tem cobertura em
`AuthServiceTests`. Se for removido no futuro, também dá pra remover
`LoginRequestDTO`'s dependência de `OrganizationId` sem quebrar nada em uso.

Essa duplicação também foi a origem de um bug real já corrigido nesta sessão:
`GenerateJwtToken` (o caminho morto) lia a secret de
`IConfiguration["Jwt:SecretKey"]`, que ficou em branco depois de uma limpeza
de segredos no `appsettings.json`; o login real (via `IJwtService`) usa
variáveis de ambiente (`JWT_SECRET`/`JWT_ISSUER`/`JWT_AUDIENCE`) propagadas
para `builder.Configuration` no `Program.cs`.

## 3. Fluxo de login (`LoginByEmailAsync`, o caminho real)

1. Busca o `User` só pelo e-mail (não filtra por organização nesse ponto).
2. Verifica a senha com BCrypt (`VerifyPassword`).
3. Se o usuário tem `OrganizationId`, pede/gera uma sessão de monitoramento ao
   vivo via `ILiveSessionService.GetOrCreateSessionForOrganizationAsync` — o
   `SessionId` retornado no login é usado pelo desktop agent para se
   identificar no WebSocket `/ws/monitor` mais tarde (ver
   [`live-monitoring.md`](live-monitoring.md)). SuperAdmins de plataforma
   (sem `OrganizationId`) recebem um `Guid.NewGuid()` avulso em vez disso.
4. Emite o JWT via `IJwtService.GenerateToken(user)` (claims: `sub`, `role`,
   `email`, e `orgId` se houver organização).
5. Retorna `AuthLoginResultDTO`: token, `ExpiresIn` (fixo em 3600s — **não
   necessariamente igual à expiração real embutida no token**, que é
   controlada por `IJwtService`, não por este valor; ver observação abaixo),
   `SessionId`, `OrganizationId`, e um resumo do usuário.

**Observação**: `ExpiresIn = 3600` é hardcoded no DTO de resposta; a expiração
real do token (`exp` claim) é decidida dentro de `IJwtService.GenerateToken`.
Se algum consumidor do login (ex.: o app desktop) usa esse `ExpiresIn` para
agendar refresh, vale conferir que os dois valores realmente batem.

## 4. Recuperação de senha

- `POST forgot-password`: sempre responde `200 OK` com a mesma mensagem
  genérica, exista ou não o e-mail (`RequestPasswordResetAsync` retorna cedo
  em silêncio se o usuário não existir) — evita enumeração de e-mails
  cadastrados.
- Gera um `PasswordResetToken` (GUID sem hífens, válido por 1 hora), monta um
  link para `{FRONTEND_URL}/reset-password?token=...` (`FRONTEND_URL` via env
  var, default `https://wisemonitor.vercel.app`) e envia por e-mail usando um
  template HTML em `html/PasswordReset.html`, via `IEmailService`.
- Falha no envio de e-mail é logada mas **não propagada** — o endpoint sempre
  responde sucesso mesmo se o SMTP falhar, para não vazar se o e-mail existe.
- `POST reset-password`: valida o token (existe, não usado, não expirado),
  troca o hash da senha e marca o token como `Used = true` (tokens são
  single-use).

## 5. Logout

`POST logout` (`[Authorize]`) lê o `Authorization: Bearer` header manualmente
(não usa `[FromHeader]`), extrai o `userId` do token via
`IJwtService.GetUserIdFromToken`, e encerra a sessão de monitoramento ao vivo
da organização inteira (`ILiveSessionService.EndSessionForOrganizationAsync`)
— é um logout **por organização**, não por usuário individual (o nome do
método no controller, "Logout realizado... para toda a organização", é
intencional, não um bug).

## 6. Rate limiting

O controller inteiro tem `[EnableRateLimiting("auth")]` — a política é
configurada centralmente (`Microsoft.AspNetCore.RateLimiting`, ver
`Program.cs`), para conter tentativas de força bruta contra login/reset.

## 7. Referência de endpoints

Base: `api/Auth`. Nenhum exige `[Authorize]` exceto `logout`.

| Método | Rota | Body | Uso |
|---|---|---|---|
| `POST` | `/login` | `{ email, password }` | Autentica, retorna JWT + `SessionId` |
| `POST` | `/forgot-password` | `{ email }` | Dispara e-mail de redefinição (sempre 200) |
| `POST` | `/reset-password` | `{ token, newPassword }` | Troca a senha usando o token recebido por e-mail |
| `POST` | `/logout` | — (`Bearer` header) | Encerra a sessão de monitoramento ao vivo da organização |

## 8. Relação com outros módulos

- Emite o `orgId` claim usado por [`users.md`](users.md), [`teams.md`](teams.md),
  [`departments.md`](departments.md) e praticamente todo módulo tenant-scoped.
- `SessionId` do login é consumido por [`live-monitoring.md`](live-monitoring.md).
- Autorização fina por permissão (não só por role) é coberta em
  [`authorization-permissions.md`](authorization-permissions.md).
