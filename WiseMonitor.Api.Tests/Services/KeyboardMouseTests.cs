using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Controllers;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.Helpers;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Repositories;
using WiseMonitor.Api.Services;
using Xunit;

namespace WiseMonitor.Api.Tests.Services;

/// <summary>
/// Tela de Teclado &amp; Mouse: consulta por dia (o front manda só a data de início),
/// sessões longas que cruzam o dia, resumo sem dados e escopo do supervisor.
/// </summary>
public class KeyboardMouseTests
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

    private static KeyboardSession Kb(Guid user, DateTime start, DateTime end, int keys, int score = 80) => new()
    {
        Id = Guid.NewGuid(), UserId = user, OrganizationId = Org, Application = "chrome",
        StartAt = start, EndAt = end, TotalKeystrokes = keys, ProductivityScore = score
    };

    // ─── Período ────────────────────────────────────────────────

    [Fact]
    public void Periodo_SemFimViraODiaInteiro()
    {
        var (from, to) = ActivityPeriod.Days(Day.AddHours(15), null);

        Assert.Equal(Day, from);
        Assert.Equal(Day.AddDays(1), to);
    }

    [Fact]
    public async Task Historico_FiltroDiaAchaSessoesDoDia()
    {
        // Antes: start=end=00:00 do dia e nada era encontrado.
        var db = CreateDb();
        db.KeyboardSessions.AddRange(
            Kb(Member, Day.AddHours(9), Day.AddHours(10), 100),
            Kb(Member, Day.AddDays(-1).AddHours(9), Day.AddDays(-1).AddHours(10), 999)); // outro dia
        db.SaveChanges();

        var repo = new KeyboardRepository(db);
        var result = (await repo.GetHistoryAsync(Member, Day, Day)).ToList();

        Assert.Single(result);
        Assert.Equal(100, result[0].TotalKeystrokes);
    }

    [Fact]
    public async Task Historico_SessaoQueComecouOntemEContinuaHojeAparece()
    {
        var db = CreateDb();
        db.KeyboardSessions.Add(Kb(Member, Day.AddHours(-3), Day.AddHours(11), 500));
        db.SaveChanges();

        var repo = new KeyboardRepository(db);

        Assert.Single(await repo.GetHistoryAsync(Member, Day, Day));
    }

    [Fact]
    public async Task Resumo_SemSessoesVoltaZeradoEClassificaPelaNota()
    {
        var db = CreateDb();
        var kb = new KeyboardRepository(db);
        var mouse = new MouseRepository(db);

        var vazio = await kb.GetSummaryAsync(Member, Day, Day);
        Assert.NotNull(vazio);
        Assert.Equal(0, vazio.TotalKeystrokes);
        Assert.NotNull(await mouse.GetSummaryAsync(Member, Day, Day));

        db.KeyboardSessions.Add(Kb(Member, Day.AddHours(9), Day.AddHours(10), 100, score: 30));
        db.SaveChanges();

        var baixo = await kb.GetSummaryAsync(Member, Day, Day);
        Assert.Equal(KeyboardClassification.Improdutivo, baixo.Classification);
    }

    // ─── Rota /sessions + escopo ────────────────────────────────

    private static ClaimsPrincipal As(Guid userId, string role) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim("userId", userId.ToString()),
            new Claim("orgId", Org.ToString()),
            new Claim(ClaimTypes.Role, role)
        }, "test"));

    private static KeyboardController Controller(AppDbContext db, ClaimsPrincipal user) =>
        new(new KeyboardService(new KeyboardRepository(db)), new AccessScopeService(db))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } }
        };

    [Fact]
    public async Task Sessoes_SupervisorRecebeSoAEquipe_AdminRecebeTodos()
    {
        var db = CreateDb();
        db.KeyboardSessions.AddRange(
            Kb(Member, Day.AddHours(9), Day.AddHours(10), 100),
            Kb(Outsider, Day.AddHours(9), Day.AddHours(10), 200));
        db.SaveChanges();

        var doSupervisor = await Controller(db, As(Supervisor, "Supervisor")).GetSessions(Day, null, null);
        var sessoesSup = Assert.IsAssignableFrom<IEnumerable<KeyboardSession>>(Assert.IsType<OkObjectResult>(doSupervisor).Value);
        Assert.Equal(new[] { Member }, sessoesSup.Select(s => s.UserId));

        var doAdmin = await Controller(db, As(Guid.NewGuid(), "admin")).GetSessions(Day, null, null);
        var sessoesAdmin = Assert.IsAssignableFrom<IEnumerable<KeyboardSession>>(Assert.IsType<OkObjectResult>(doAdmin).Value);
        Assert.Equal(2, sessoesAdmin.Count());
    }

    [Fact]
    public async Task Sessoes_SupervisorNaoConsultaUsuarioForaDaEquipe()
    {
        var db = CreateDb();

        var result = await Controller(db, As(Supervisor, "Supervisor")).GetSessions(Day, null, Outsider);

        Assert.IsType<ForbidResult>(result);
    }
}
