using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Services;
using WiseMonitor.Api.Utils;
using Xunit;

namespace WiseMonitor.Api.Tests.Services;

/// <summary>
/// Supervisor enxerga só as equipes que administra (membros + ele mesmo) e só mexe
/// nas jornadas usadas exclusivamente por elas. Demais papéis veem a org inteira.
/// </summary>
public class AccessScopeServiceTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid OtherOrg = Guid.NewGuid();

    // Supervisor administra a Equipe A (como responsável) e a Equipe B (como
    // administrador-membro). A Equipe C é de outro supervisor.
    private static readonly Guid Supervisor = Guid.NewGuid();
    private static readonly Guid OtherSupervisor = Guid.NewGuid();
    private static readonly Guid MemberA = Guid.NewGuid();
    private static readonly Guid MemberB = Guid.NewGuid();
    private static readonly Guid MemberC = Guid.NewGuid();
    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly Guid Stranger = Guid.NewGuid(); // outra organização

    private static readonly Guid TeamA = Guid.NewGuid();
    private static readonly Guid TeamB = Guid.NewGuid();
    private static readonly Guid TeamC = Guid.NewGuid();

    private static AppDbContext CreateDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        User U(Guid id, string role, Guid? org = null) => new()
        {
            Id = id, FirstName = role, LastName = "Teste", Email = $"{id}@x.com",
            PasswordHash = "x", Role = role, IsActive = true, OrganizationId = org ?? Org
        };

        db.Users.AddRange(
            U(Supervisor, "Supervisor"), U(OtherSupervisor, "Supervisor"),
            U(MemberA, "Employee"), U(MemberB, "Employee"), U(MemberC, "Employee"),
            U(Admin, "admin"), U(Stranger, "Employee", OtherOrg));

        db.Teams.AddRange(
            new Team
            {
                Id = TeamA, OrganizationId = Org, Name = "A", ManagerId = Supervisor,
                Members = { new TeamMember { UserId = MemberA } }
            },
            new Team
            {
                Id = TeamB, OrganizationId = Org, Name = "B", ManagerId = OtherSupervisor,
                Members =
                {
                    new TeamMember { UserId = Supervisor, IsManager = true },
                    new TeamMember { UserId = MemberB }
                }
            },
            new Team
            {
                Id = TeamC, OrganizationId = Org, Name = "C", ManagerId = OtherSupervisor,
                Members = { new TeamMember { UserId = MemberC } }
            });

        db.SaveChanges();
        return db;
    }

    private static ClaimsPrincipal As(Guid userId, string role, Guid? org = null) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim("userId", userId.ToString()),
            new Claim("orgId", (org ?? Org).ToString()),
            new Claim(ClaimTypes.Role, role)
        }, "test"));

    // ─── Escopo de usuários/equipes ─────────────────────────────

    [Fact]
    public async Task Supervisor_VeSoMembrosDasEquipesQueAdministra()
    {
        var svc = new AccessScopeService(CreateDb());

        var scope = await svc.GetScopeAsync(As(Supervisor, "Supervisor"));

        Assert.True(scope.IsRestricted);
        Assert.True(scope.CanAccessUser(Supervisor));
        Assert.True(scope.CanAccessUser(MemberA));          // equipe onde é responsável
        Assert.True(scope.CanAccessUser(MemberB));          // equipe onde é administrador
        Assert.False(scope.CanAccessUser(MemberC));         // equipe de outro supervisor
        Assert.False(scope.CanAccessUser(Admin));
        Assert.True(scope.CanAccessTeam(TeamA));
        Assert.True(scope.CanAccessTeam(TeamB));
        Assert.False(scope.CanAccessTeam(TeamC));
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("TenantAdmin")]
    [InlineData("Manager")]
    public async Task OutrosPapeis_NaoSaoRestritos(string role)
    {
        var svc = new AccessScopeService(CreateDb());

        var scope = await svc.GetScopeAsync(As(Admin, role));

        Assert.False(scope.IsRestricted);
        Assert.True(scope.CanAccessUser(MemberC));
        Assert.True(scope.CanAccessTeam(TeamC));
    }

    [Fact]
    public async Task CanAccessUser_BloqueiaOutraOrganizacaoMesmoSemRestricao()
    {
        var svc = new AccessScopeService(CreateDb());

        Assert.True(await svc.CanAccessUserAsync(As(Admin, "admin"), MemberC));
        Assert.False(await svc.CanAccessUserAsync(As(Admin, "admin"), Stranger));
    }

    [Fact]
    public async Task CanAccessUser_SupervisorNaoAcessaForaDasEquipes()
    {
        var svc = new AccessScopeService(CreateDb());
        var sup = As(Supervisor, "Supervisor");

        Assert.True(await svc.CanAccessUserAsync(sup, MemberA));
        Assert.False(await svc.CanAccessUserAsync(sup, MemberC));
    }

    // ─── Dispositivos ───────────────────────────────────────────

    [Fact]
    public async Task Device_AtribuicaoManualTemPrioridadeSobreCaptura()
    {
        var db = CreateDb();
        var deviceId = Guid.NewGuid();

        // Última captura diz MemberA, mas um admin atribuiu a máquina ao MemberC.
        db.Devices.Add(new Device { Id = deviceId, OrganizationId = Org, Hostname = "pc", UserId = MemberC });
        db.Screenshots.Add(new Screenshot
        {
            OrganizationId = Org, DeviceId = deviceId.ToString(), MonitoredUserId = MemberA, ImageData = new byte[1]
        });
        db.SaveChanges();

        var svc = new AccessScopeService(db);

        Assert.Equal(MemberC, await svc.ResolveDeviceUserIdAsync(deviceId.ToString(), Org));
        Assert.False(await svc.CanAccessDeviceAsync(As(Supervisor, "Supervisor"), deviceId.ToString()));
    }

    [Fact]
    public async Task Device_SemAtribuicaoUsaUltimaCaptura()
    {
        var db = CreateDb();
        const string deviceId = "maquina-1";
        db.Screenshots.AddRange(
            new Screenshot { OrganizationId = Org, DeviceId = deviceId, MonitoredUserId = MemberC, ImageData = new byte[1], CapturedAt = DateTime.UtcNow.AddHours(-1) },
            new Screenshot { OrganizationId = Org, DeviceId = deviceId, MonitoredUserId = MemberA, ImageData = new byte[1], CapturedAt = DateTime.UtcNow });
        db.SaveChanges();

        var svc = new AccessScopeService(db);

        Assert.True(await svc.CanAccessDeviceAsync(As(Supervisor, "Supervisor"), deviceId));
    }

    // ─── Jornadas ───────────────────────────────────────────────

    private static Guid AddSchedule(AppDbContext db, Guid? createdBy = null, Guid? teamId = null, Guid? userId = null)
    {
        var id = Guid.NewGuid();
        db.WorkSchedules.Add(new WorkSchedule { Id = id, OrganizationId = Org, Name = id.ToString(), CreatedByUserId = createdBy });
        if (teamId.HasValue)
            db.Teams.Find(teamId.Value)!.DefaultWorkScheduleId = id;
        if (userId.HasValue)
            db.UserWorkSchedules.Add(new UserWorkSchedule { OrganizationId = Org, UserId = userId.Value, WorkScheduleId = id });
        db.SaveChanges();
        return id;
    }

    [Fact]
    public async Task Jornada_RegrasDoSupervisor()
    {
        var db = CreateDb();
        var modelo        = AddSchedule(db);                                  // ninguém usa (ex.: padrão)
        var daMinhaEquipe = AddSchedule(db, teamId: TeamA);
        var deOutraEquipe = AddSchedule(db, teamId: TeamC);
        var compartilhada = AddSchedule(db, teamId: TeamB, userId: MemberC);  // minha equipe + alguém de fora
        var criadaPorMim  = AddSchedule(db, createdBy: Supervisor);
        var doMeuMembro   = AddSchedule(db, userId: MemberA);

        var svc = new AccessScopeService(db);
        var sup = As(Supervisor, "Supervisor");

        Assert.Equal(ScheduleAccess.Read,   await svc.GetScheduleAccessAsync(sup, modelo));
        Assert.Equal(ScheduleAccess.Manage, await svc.GetScheduleAccessAsync(sup, daMinhaEquipe));
        Assert.Equal(ScheduleAccess.Hidden, await svc.GetScheduleAccessAsync(sup, deOutraEquipe));
        Assert.Equal(ScheduleAccess.Read,   await svc.GetScheduleAccessAsync(sup, compartilhada));
        Assert.Equal(ScheduleAccess.Manage, await svc.GetScheduleAccessAsync(sup, criadaPorMim));
        Assert.Equal(ScheduleAccess.Manage, await svc.GetScheduleAccessAsync(sup, doMeuMembro));

        var visible = await svc.GetVisibleSchedulesAsync(sup);
        Assert.Equal(5, visible.Count);
        Assert.False(visible.ContainsKey(deOutraEquipe));
    }

    [Fact]
    public async Task Jornada_AdminGerenciaTodasDaOrgENenhumaDeFora()
    {
        var db = CreateDb();
        var deOutraEquipe = AddSchedule(db, teamId: TeamC);
        var deOutraOrg = Guid.NewGuid();
        db.WorkSchedules.Add(new WorkSchedule { Id = deOutraOrg, OrganizationId = OtherOrg, Name = "x" });
        db.SaveChanges();

        var svc = new AccessScopeService(db);
        var admin = As(Admin, "admin");

        Assert.Equal(ScheduleAccess.Manage, await svc.GetScheduleAccessAsync(admin, deOutraEquipe));
        Assert.Equal(ScheduleAccess.NotFound, await svc.GetScheduleAccessAsync(admin, deOutraOrg));
    }

    // ─── Validador de jornada ───────────────────────────────────

    private static WorkScheduleCreateDTO Dto(int day, int start, int end, bool crossesMidnight = false) => new()
    {
        Name = "x",
        Type = 2,
        Rules = new List<WorkScheduleRuleDTO>
        {
            new() { Day = day, StartTimeMinutes = start, EndTimeMinutes = end, BreakDurationMinutes = 60, CrossesMidnight = crossesMidnight }
        }
    };

    [Fact]
    public void Validador_AceitaDomingo() =>
        WorkScheduleValidator.ValidateCreate(Dto(day: 7, start: 9 * 60, end: 17 * 60));

    [Fact]
    public void Validador_AceitaJornadaQuePassaDaMeiaNoite() =>
        WorkScheduleValidator.ValidateCreate(Dto(day: 1, start: 15 * 60 + 40, end: 0, crossesMidnight: true));

    [Fact]
    public void Validador_RecusaFimAntesDoInicioSemMeiaNoite() =>
        Assert.Throws<ArgumentException>(() =>
            WorkScheduleValidator.ValidateCreate(Dto(day: 1, start: 15 * 60, end: 9 * 60)));

    // ─── Ao vivo (WebSocket) ────────────────────────────────────

    private sealed class FakeWebSocket : WebSocket
    {
        public ConcurrentQueue<byte[]> Sent { get; } = new();
        public override WebSocketState State => WebSocketState.Open;
        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType t, bool end, CancellationToken c)
        {
            Sent.Enqueue(buffer.ToArray());
            return Task.CompletedTask;
        }
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override string? SubProtocol => null;
        public override void Abort() { }
        public override Task CloseAsync(WebSocketCloseStatus s, string? d, CancellationToken c) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus s, string? d, CancellationToken c) => Task.CompletedTask;
        public override void Dispose() { }
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> b, CancellationToken c)
            => Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Text, true));
    }

    private static List<string> LastUpdateDeviceIds(FakeWebSocket ws)
    {
        var last = ws.Sent.Select(b => JsonDocument.Parse(Encoding.UTF8.GetString(b)))
            .Last(d => d.RootElement.GetProperty("eventType").GetString() == "update");
        return last.RootElement.GetProperty("payload").EnumerateArray()
            .Select(d => d.GetProperty("deviceId").GetString()!)
            .ToList();
    }

    [Fact]
    public async Task AoVivo_ConexaoDoSupervisorRecebeSoMaquinasDoEscopo()
    {
        var live = new LiveMonitoringService(Microsoft.Extensions.Logging.Abstractions.NullLogger<LiveMonitoringService>.Instance);
        var org = Org.ToString();
        var adminWs = new FakeWebSocket();
        var supWs = new FakeWebSocket();

        live.RegisterAdmin(org, "admin", adminWs);
        live.RegisterAdmin(org, "sup", supWs, new HashSet<string> { MemberA.ToString() });

        live.RegisterOrUpdateDevice(new LiveDeviceUpdateDTO { DeviceId = "pc-a", OrgId = org, UserId = MemberA.ToString() });
        live.RegisterOrUpdateDevice(new LiveDeviceUpdateDTO { DeviceId = "pc-c", OrgId = org, UserId = MemberC.ToString() });
        await Task.Delay(100); // broadcast é disparado sem await

        Assert.Equal(new[] { "pc-a", "pc-c" }, LastUpdateDeviceIds(adminWs).OrderBy(x => x));
        Assert.Equal(new[] { "pc-a" }, LastUpdateDeviceIds(supWs));

        Assert.True(live.CanSessionSeeDevice("sup", "pc-a"));
        Assert.False(live.CanSessionSeeDevice("sup", "pc-c"));
        Assert.True(live.CanSessionSeeDevice("admin", "pc-c"));
        Assert.Equal(new[] { "pc-a" }, live.GetAllLiveDevicesForSession(org, "sup").Select(d => d.DeviceId));
    }
}
