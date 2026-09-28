using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WiseMonitor.Api.Controllers;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Repositories;
using WiseMonitor.Api.Services;
using Xunit;

namespace WiseMonitor.Api.Tests.Services;

/// <summary>
/// Tela de Atividade (geral / por equipe / por usuário) e o histórico de vídeo
/// usado para tocar o vídeo de uma atividade.
/// </summary>
public class ActivityTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Supervisor = Guid.NewGuid();
    private static readonly Guid Member = Guid.NewGuid();
    private static readonly Guid Outsider = Guid.NewGuid();
    private static readonly DateTime Day = new(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);

    private static AppDbContext CreateDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        foreach (var (id, role) in new[] { (Supervisor, "Supervisor"), (Member, "Employee"), (Outsider, "Employee") })
            db.Users.Add(new User
            {
                Id = id, FirstName = role, LastName = "T", Email = $"{id}@x.com",
                PasswordHash = "x", Role = role, IsActive = true, OrganizationId = Org
            });

        db.Teams.Add(new Team
        {
            OrganizationId = Org, Name = "Equipe", ManagerId = Supervisor,
            Members = { new TeamMember { UserId = Member } }
        });

        db.SaveChanges();
        return db;
    }

    private static AppFocusEvent Ev(Guid user, DateTime start, string app = "chrome", string? icon = "ICON") => new()
    {
        Id = Guid.NewGuid(), OrganizationId = Org, UserId = user, DeviceId = Guid.NewGuid(),
        ApplicationName = app, ProcessName = app, WindowTitle = app, IconBase64 = icon,
        StartTime = start, EndTime = start.AddMinutes(5), DurationSeconds = 300
    };

    private static ClaimsPrincipal As(Guid userId, string role) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim("userId", userId.ToString()),
            new Claim("orgId", Org.ToString()),
            new Claim(ClaimTypes.Role, role)
        }, "test"));

    private static AppFocusController Controller(AppDbContext db, ClaimsPrincipal user)
    {
        var service = new AppFocusService(new AppFocusRepository(db), null!, null!, NullLogger<AppFocusService>.Instance);
        return new AppFocusController(service, new AccessScopeService(db), NullLogger<AppFocusController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } }
        };
    }

    private static List<AppFocusEventResponseDTO> Events(IActionResult result) =>
        Assert.IsAssignableFrom<IEnumerable<AppFocusEventResponseDTO>>(Assert.IsType<OkObjectResult>(result).Value).ToList();

    // ─── Atividade geral / equipe / usuário ─────────────────────

    [Fact]
    public async Task Geral_FiltroDiaTrazODiaInteiroENaoOsOutros()
    {
        // Antes: startDate == endDate (00:00) não achava nada no dia.
        var db = CreateDb();
        db.AppFocusEvents.AddRange(
            Ev(Member, Day.AddHours(9)),
            Ev(Member, Day.AddHours(23).AddMinutes(30)),
            Ev(Member, Day.AddDays(1).AddHours(1)));
        db.SaveChanges();

        var result = Events(await Controller(db, As(Guid.NewGuid(), "admin")).GetAll(Day, null, null));

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task Supervisor_VeSoAEquipe_EFiltroPorUsuarioDeForaENegado()
    {
        var db = CreateDb();
        db.AppFocusEvents.AddRange(Ev(Member, Day.AddHours(9)), Ev(Outsider, Day.AddHours(9)));
        db.SaveChanges();

        var sup = Controller(db, As(Supervisor, "Supervisor"));

        Assert.Equal(new[] { Member }, Events(await sup.GetAll(Day, null, null)).Select(e => e.UserId));
        Assert.IsType<ForbidResult>(await sup.GetAll(Day, null, Outsider));
        Assert.Single(Events(await sup.GetAll(Day, null, Member)));
    }

    [Fact]
    public async Task Geral_IconeVaiUmaVezPorPrograma()
    {
        var db = CreateDb();
        db.AppFocusEvents.AddRange(
            Ev(Member, Day.AddHours(9), "chrome"),
            Ev(Member, Day.AddHours(10), "chrome"),
            Ev(Member, Day.AddHours(11), "excel"));
        db.SaveChanges();

        var result = Events(await Controller(db, As(Guid.NewGuid(), "admin")).GetAll(Day, null, null));

        Assert.Equal(1, result.Count(e => e.ProcessName == "chrome" && e.IconBase64 != null));
        Assert.Equal(1, result.Count(e => e.ProcessName == "excel" && e.IconBase64 != null));
    }

    // ─── Histórico de vídeo (vídeo da atividade) ────────────────

    [Fact]
    public async Task HistoricoDeVideo_NaoCarregaOsBytesEEnxugaOContexto()
    {
        var db = CreateDb();
        const string device = "pc-1";
        db.VideoSegments.Add(new VideoSegment
        {
            OrganizationId = Org, MonitoredUserId = Member, DeviceId = device,
            StartedAt = Day.AddHours(9), EndedAt = Day.AddHours(9).AddSeconds(10),
            VideoData = new byte[1024 * 1024]
        });
        db.AppFocusEvents.Add(Ev(Member, Day.AddHours(9)));
        db.KeyboardSessions.Add(new KeyboardSession
        {
            Id = Guid.NewGuid(), UserId = Member, OrganizationId = Org, Application = "chrome",
            StartAt = Day.AddHours(9), EndAt = Day.AddHours(9).AddMinutes(1), TotalKeystrokes = 42,
            Words = { new KeyboardWord { Id = Guid.NewGuid(), Word = "ola", Count = 1 } }
        });
        db.SaveChanges();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["VideoSegmentRetentionHours"] = "4",
                ["Jwt:SecretKey"] = new string('k', 64)
            })
            .Build();

        var repo = new VideoSegmentRepository(db);

        // Listagem só com metadados; o vídeo da atividade precisa dos bytes.
        var semBytes = (await repo.GetHistoryAsync(device, Day, Day.AddDays(1))).Single();
        Assert.Empty(semBytes.VideoData);
        var comBytes = (await repo.GetHistoryWithDataAsync(device, Day, Day.AddDays(1))).Single();
        Assert.Equal(1024 * 1024, comBytes.VideoData.Length);

        var live = new LiveMonitoringService(NullLogger<LiveMonitoringService>.Instance);
        var service = new VideoSegmentService(repo, db, live, config);

        var item = (await service.GetHistoryWithContextAsync(device, Day.AddHours(8), Day.AddHours(10), "https://api")).Single();

        Assert.Contains("/api/video-segments/", item.Segment.Url);
        Assert.Single(item.Context.AppFocusEvents);
        var sessao = Assert.Single(item.Context.KeyboardSessions);
        Assert.Equal(42, sessao.TotalKeystrokes);
        Assert.Empty(sessao.TopWords);
    }
}
