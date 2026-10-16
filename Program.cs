using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using DotNetEnv;
using Serilog;

using WiseMonitor.Api.Authorization;
using WiseMonitor.Api.Config;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.Models.Enums;
using WiseMonitor.Api.Repositories;
using WiseMonitor.Api.Services;
using WiseMonitor.Api.Services.Reports;
using WiseMonitor.Api.Middlewares;
using WiseMonitor.Api.Helpers;
using WiseMonitor.Api.Extensions;
using Microsoft.AspNetCore.Mvc;

// =======================
// LOAD .ENV (antes do Serilog para poder ler LOG_DIRECTORY etc. se vier do .env)
// =======================
Env.Load();

// =======================
// SERILOG (console + arquivo)
// =======================
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(AppContext.BaseDirectory, "logs", "wisemonitor-api-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14)
    .CreateLogger();

try
{

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// =======================
// JWT
// =======================
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");

if (string.IsNullOrWhiteSpace(jwtSecret))
    throw new Exception("JWT_SECRET não configurado em produção!");

// HMAC-SHA256 com chave curta é quebrável por força bruta offline a partir de
// qualquer token emitido — e a mesma chave assina as URLs de vídeo/captura.
if (Encoding.UTF8.GetByteCount(jwtSecret) < 32)
    throw new Exception("JWT_SECRET precisa ter pelo menos 32 bytes (use um valor aleatório de 64+ caracteres).");

var jwtSettings = new JwtSettings
{
    SecretKey          = jwtSecret,
    Issuer             = Environment.GetEnvironmentVariable("JWT_ISSUER"),
    Audience           = Environment.GetEnvironmentVariable("JWT_AUDIENCE"),
    ExpirationMinutes  = int.Parse(Environment.GetEnvironmentVariable("JWT_EXPIRATION_MINUTES") ?? "60")
};

builder.Services.AddSingleton(jwtSettings);
builder.Services.AddHttpClient();

var key = Encoding.UTF8.GetBytes(jwtSettings.SecretKey);

// JwtService (usado no login para EMITIR tokens) lê "Jwt:*" via IConfiguration —
// propaga os mesmos valores do JWT_SECRET/ISSUER/AUDIENCE (env) para lá também,
// já que appsettings.json nunca deve conter o segredo real.
builder.Configuration["Jwt:SecretKey"] = jwtSettings.SecretKey;
builder.Configuration["Jwt:Issuer"] = jwtSettings.Issuer;
builder.Configuration["Jwt:Audience"] = jwtSettings.Audience;

// =======================
// SMTP (senha via env var, nunca em appsettings.json)
// =======================
var smtpPass = Environment.GetEnvironmentVariable("SMTP_PASS");
if (!string.IsNullOrWhiteSpace(smtpPass))
    builder.Configuration["Smtp:Pass"] = smtpPass;

// =======================
// VIDEO SEGMENTS
// =======================
var videoRetentionHours = Environment.GetEnvironmentVariable("VIDEO_SEGMENT_RETENTION_HOURS");
if (!string.IsNullOrWhiteSpace(videoRetentionHours))
    builder.Configuration["VideoSegmentRetentionHours"] = videoRetentionHours;

// =======================
// DATABASE
// =======================
var dbHost = Environment.GetEnvironmentVariable("DB_HOST");
var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
var dbName = Environment.GetEnvironmentVariable("DB_NAME");
var dbUser = Environment.GetEnvironmentVariable("DB_USER");
var dbPass = Environment.GetEnvironmentVariable("DB_PASSWORD");

if (string.IsNullOrWhiteSpace(dbHost))
    throw new Exception("DB_HOST não configurado!");

var connectionString =
    $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPass};Search Path=public;Pooling=true;Timeout=15;CommandTimeout=30";

Log.Information("[DB] Conectando → {DbHost}:{DbPort}", dbHost, dbPort);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

// =======================
// HEALTH CHECKS
// =======================
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgres", tags: new[] { "ready" });

// =======================
// RATE LIMITING
// =======================
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Login/forgot-password: alvo primário de força bruta/enumeração de e-mails.
    // Particionado por IP: um AddFixedWindowLimiter simples é UM balde para a
    // plataforma inteira — 10 requisições de um atacante travavam o login de todos.
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Limite global sensato por IP para o restante da API.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// =======================
// SERVICES
// =======================
builder.Services.AddSingleton<LiveMonitoringService>();
builder.Services.AddSingleton<ILiveMonitoringService>(sp => sp.GetRequiredService<LiveMonitoringService>());
builder.Services.AddSingleton<LiveFrameRelay>();
builder.Services.AddHostedService<DevicePresenceSweeper>();

builder.Services.AddScoped<IAuthService,                AuthService>();
builder.Services.AddScoped<IEmailService,               EmailService>();
builder.Services.AddScoped<IJwtService,                 JwtService>();
builder.Services.AddScoped<IRefreshTokenService,        RefreshTokenService>();
builder.Services.AddScoped<IUserService,                UserService>();
builder.Services.AddScoped<IAccessScopeService,         AccessScopeService>();
builder.Services.AddScoped<IOrganizationService,        OrganizationService>();
builder.Services.AddScoped<IDeviceService,              DeviceService>();
builder.Services.AddScoped<IScreenshotService,          ScreenshotService>();
builder.Services.AddScoped<IVideoSegmentService,        VideoSegmentService>();
builder.Services.AddScoped<IAppFocusService,            AppFocusService>();
builder.Services.AddScoped<IReportDataService,          ReportDataService>();
builder.Services.AddScoped<IReportPdfService,           ReportPdfService>();
builder.Services.AddScoped<IReportCsvService,           ReportCsvService>();
builder.Services.AddScoped<IActivityClassificationService, ActivityClassificationService>();
builder.Services.AddScoped<IWorkScheduleService,        WorkScheduleService>();
builder.Services.AddScoped<ITeamService,                TeamService>();
builder.Services.AddScoped<IKeyboardService,            KeyboardService>();
builder.Services.AddScoped<IMouseService,               MouseService>();
builder.Services.AddScoped<IApplicationDisplayNameService, ApplicationDisplayNameService>();
builder.Services.AddScoped<IProductivityClassificationService, ProductivityClassificationService>();
builder.Services.AddScoped<ILiveSessionService,         LiveSessionService>();

// Novos serviços
builder.Services.AddScoped<IDepartmentService,  DepartmentService>();
builder.Services.AddScoped<IAuditService,       AuditService>();
builder.Services.AddScoped<IEntitlementService, EntitlementService>();
builder.Services.AddScoped<IDelegationService,  DelegationService>();

// SuperAdmin
builder.Services.AddScoped<ISuperAdminTenantService,      SuperAdminTenantService>();
builder.Services.AddScoped<ISuperAdminUserService,        SuperAdminUserService>();
builder.Services.AddScoped<ISuperAdminMetricsService,     SuperAdminMetricsService>();
builder.Services.AddScoped<ISuperAdminAlertService,       SuperAdminAlertService>();
builder.Services.AddScoped<ISuperAdminAgentService,       SuperAdminAgentService>();
builder.Services.AddScoped<ISuperAdminIntegrationService, SuperAdminIntegrationService>();
builder.Services.AddScoped<ISuperAdminSettingsService,    SuperAdminSettingsService>();
builder.Services.AddScoped<SuperAdminAuthorizationFilter>();

// Agent module
builder.Services.AddAgentModule(builder.Configuration);

// =======================
// REPOSITORIES
// =======================
builder.Services.AddScoped<IUserRepository,         UserRepository>();
builder.Services.AddScoped<IOrganizationRepository, OrganizationRepository>();
builder.Services.AddScoped<IDeviceRepository,       DeviceRepository>();
builder.Services.AddScoped<IScreenshotRepository,   ScreenshotRepository>();
builder.Services.AddScoped<IVideoSegmentRepository, VideoSegmentRepository>();
builder.Services.AddScoped<IWorkScheduleRepository, WorkScheduleRepository>();
builder.Services.AddScoped<IAppFocusRepository,     AppFocusRepository>();
builder.Services.AddScoped<ITeamRepository,         TeamRepository>();
builder.Services.AddScoped<IKeyboardRepository,     KeyboardRepository>();
builder.Services.AddScoped<IMouseRepository,        MouseRepository>();
builder.Services.AddScoped<IApplicationDisplayNameRepository, ApplicationDisplayNameRepository>();
builder.Services.AddScoped<IProductivityClassificationRepository, ProductivityClassificationRepository>();

// =======================
// WebRTC
// =======================
builder.Services.AddSingleton<LiveStreamHub>();

// =======================
// Helpers / Infra
// =======================
builder.Services.AddSingleton<JwtHelper>();
builder.Services.AddSingleton<LoginAttemptTracker>();

// Cloud Run/load balancer: sem isso RemoteIpAddress é o IP do proxy do Google, e o
// rate limit por IP vira um balde só para todos os clientes. ForwardLimit = 1 usa só
// o último hop (o que o próprio proxy acrescentou), então o cliente não forja o IP.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                             | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, TenantContext>();

// =======================
// Controllers
// =======================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// =======================
// CORS
// =======================
// ALLOWED_ORIGINS: lista separada por vírgula (ex.: https://app.wisemonitor.com,https://admin.wisemonitor.com)
// Em desenvolvimento, sem a env var, libera localhost para não travar o dia a dia.
var allowedOriginsRaw = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS");
var allowedOrigins = string.IsNullOrWhiteSpace(allowedOriginsRaw)
    ? (builder.Environment.IsDevelopment()
        ? new[] { "http://localhost:3000", "http://localhost:5173", "http://localhost:5174" }
        : Array.Empty<string>())
    : allowedOriginsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// =======================
// AUTHENTICATION (JWT)
// =======================
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = true;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey         = new SymmetricSecurityKey(key),
        ValidateIssuer           = true,
        ValidIssuer              = jwtSettings.Issuer,
        ValidateAudience         = true,
        ValidAudience            = jwtSettings.Audience,
        ClockSkew                = TimeSpan.Zero
    };

    // Suporte a JWT via query string — só no handshake de WebSocket, onde o navegador
    // não consegue mandar header. Em rota HTTP comum o token na URL vaza em log de
    // proxy, histórico do navegador e header Referer.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            if (!ctx.Request.Path.StartsWithSegments("/ws"))
                return Task.CompletedTask;

            var token = ctx.Request.Query["token"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(token))
                ctx.Token = token;
            return Task.CompletedTask;
        }
    };
});

// =======================
// AUTHORIZATION (RBAC + Permission Policies)
// =======================
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddAuthorization(options =>
{
    // SuperAdmin da plataforma
    options.AddPolicy("SuperAdminOnly", policy =>
        policy.RequireRole(UserRoles.SuperAdmin));

    // Cria uma política "Permission:<perm>" para cada permissão conhecida
    string[] allPermissions =
    [
        Permissions.UsersCreate,       Permissions.UsersEdit,       Permissions.UsersDelete,   Permissions.UsersView,
        Permissions.DepartmentsCreate, Permissions.DepartmentsEdit, Permissions.DepartmentsDelete,
        Permissions.TeamsCreate,       Permissions.TeamsEdit,       Permissions.TeamsDelete,
        Permissions.SchedulesManage,
        Permissions.ProjectsCreate,    Permissions.ProjectsEdit,    Permissions.ProjectsDelete,
        Permissions.ReportsView,       Permissions.ReportsExport,
        Permissions.ScreenshotsView,   Permissions.ScreenshotsDelete,
        Permissions.ActivityView,      Permissions.ActivityExport,
        Permissions.LivestreamView,    Permissions.LivestreamStart,  Permissions.LivestreamStop,
        Permissions.SystemMetricsView,
        Permissions.BillingView,       Permissions.BillingManage,
        Permissions.AuditLogsView,
        Permissions.RolesManage,       Permissions.PermissionsManage,
        Permissions.DelegationsCreate, Permissions.DelegationsView,
        Permissions.TenantsView,       Permissions.TenantsCreate,    Permissions.TenantsEdit,
        Permissions.TenantsDelete,     Permissions.TenantsSuspend,   Permissions.MetricsGlobal,
    ];

    foreach (var perm in allPermissions)
    {
        var captured = perm;
        options.AddPolicy($"Permission:{captured}", policy =>
            policy.Requirements.Add(new PermissionRequirement(captured)));
    }
});

var app = builder.Build();

// Resolve LiveFrameRelay logo no startup para que ele já esteja inscrito em
// ILiveMonitoringService.WatchStateChanged antes da primeira conexão de agent —
// senão um "watch-start" perdido faria o agent nunca começar a transmitir.
app.Services.GetRequiredService<LiveFrameRelay>();

// =======================
// PORT CLOUD RUN
// =======================
var port = Environment.GetEnvironmentVariable("APP_PORT") ?? "8080";
app.Urls.Add($"http://0.0.0.0:{port}");
Log.Information("API rodando na porta {Port}", port);

// =======================
// Swagger (somente Development)
// =======================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// =======================
// MIDDLEWARES
// =======================
app.UseForwardedHeaders();
app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseRouting();
app.UseCors("Default");
app.UseRateLimiter();
app.UseWebSockets();
app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<AuditMiddleware>();
app.UseMiddleware<DeviceWebSocketMiddleware>();
app.UseMiddleware<MonitorWebSocketMiddleware>();
app.UseMiddleware<AgentStreamWebSocketMiddleware>();
app.UseMiddleware<TenantMiddleware>();

app.UseStaticFiles();
app.MapControllers().RequireCors("Default");

// =======================
// HEALTH CHECK ENDPOINTS
// =======================
// /health       → liveness simples (o processo está de pé)
// /health/ready → readiness (banco de dados acessível) — usado pelo Cloud Run/orquestrador
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// =======================
// MIGRATION SEGURA
// =======================
// AUTO_MIGRATE: precisa ser "true" explicitamente para migrar no start (padrão seguro para produção).
// Em Development, migra automaticamente por conveniência a menos que AUTO_MIGRATE=false.
var autoMigrateRaw = Environment.GetEnvironmentVariable("AUTO_MIGRATE");
var autoMigrate = autoMigrateRaw != null
    ? string.Equals(autoMigrateRaw, "true", StringComparison.OrdinalIgnoreCase)
    : app.Environment.IsDevelopment();

if (autoMigrate)
{
    using var scope = app.Services.CreateScope();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Log.Information("[DB] Aplicando migrations...");

        if (db.Database.CanConnect())
        {
            // Criação de índice em tabela grande passa fácil dos 30s padrão.
            db.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
            db.Database.Migrate();
            Log.Information("[DB] Banco atualizado com sucesso");
        }
        else
        {
            Log.Warning("[DB] Não conseguiu conectar no banco");
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "[DB] Erro ao aplicar migrations");
    }
}
else
{
    Log.Information("[DB] AUTO_MIGRATE desativado — rode as migrations como um passo separado do deploy.");
}

// =======================
// SEED SUPER ADMIN
// =======================
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var superAdminEmail = Environment.GetEnvironmentVariable("SUPERADMIN_EMAIL") ?? "superadmin@wisemonitor.com";
        var superAdminPassword = Environment.GetEnvironmentVariable("SUPERADMIN_PASSWORD");

        // O repositório é público: uma senha padrão aqui é a senha do SuperAdmin de
        // qualquer ambiente que esqueceu de definir SUPERADMIN_PASSWORD.
        if (string.IsNullOrWhiteSpace(superAdminPassword) && app.Environment.IsDevelopment())
            superAdminPassword = "SuperAdmin@2025!";

        if (!db.Users.Any(u => u.IsSuperAdmin) && string.IsNullOrWhiteSpace(superAdminPassword))
        {
            Log.Warning("[SEED] Nenhum SuperAdmin existe e SUPERADMIN_PASSWORD não está definida — seed ignorado.");
        }
        else if (!db.Users.Any(u => u.IsSuperAdmin))
        {
            db.Users.Add(new WiseMonitor.Api.Models.User
            {
                FirstName    = "Super",
                LastName     = "Admin",
                Email        = superAdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(superAdminPassword),
                Role         = WiseMonitor.Api.Models.Enums.UserRoles.SuperAdmin,
                IsSuperAdmin = true,
                IsActive     = true,
                OrganizationId = null,
            });

            db.SaveChanges();
            Log.Information("[SEED] SuperAdmin criado → {SuperAdminEmail}", superAdminEmail);
            Log.Information("[SEED] Senha definida via SUPERADMIN_PASSWORD (ou padrão de fallback) — nunca logada aqui.");
            Log.Warning("[SEED] Troque a senha após o primeiro login!");
        }
        else
        {
            Log.Information("[SEED] SuperAdmin já existe — nenhuma ação necessária.");
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "[SEED] Erro ao criar SuperAdmin");
    }
}

app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "WiseMonitor.Api encerrou de forma inesperada durante o startup");
}
finally
{
    Log.CloseAndFlush();
}

