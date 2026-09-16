using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.DTOs.Reports;
using WiseMonitor.Api.DTOs.Team;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Repositories;
using WiseMonitor.Api.Services;
using WiseMonitor.Api.Services.Reports;
using Xunit;

namespace WiseMonitor.Api.Tests.Services;

file class FakeTenantContext : ITenantContext
{
    public Guid? OrganizationId { get; init; }
    public bool IsSuperAdmin { get; init; }
    public bool IsActive { get; init; }
}

class FakeAppFocusService : IAppFocusService
{
    public List<AppFocusEvent> Events { get; } = new();

    public Task<IEnumerable<AppFocusEvent>> GetHistoryAsync(Guid userId, DateTime start, DateTime end)
        => Task.FromResult(Events.Where(e => e.UserId == userId));

    public Task RegisterEventAsync(AppFocusEventCreateDTO dto, Guid userId, Guid organizationId) => throw new NotImplementedException();
    public Task<IEnumerable<AppFocusEventResponseDTO>> GetAllAsync(Guid organizationId, DateTime startDate, DateTime endDate) => throw new NotImplementedException();
    public Task<AppFocusEventResponseDTO?> GetByIdAsync(Guid id, Guid organizationId) => throw new NotImplementedException();
    public Task UpdateAsync(Guid id, AppFocusEventUpdateDTO dto, Guid organizationId) => throw new NotImplementedException();
    public Task DeleteAsync(Guid id, Guid organizationId) => throw new NotImplementedException();
    public Task<IEnumerable<object>> GetMetricsAsync(Guid userId, DateTime date) => throw new NotImplementedException();
}

class FakeTeamService : ITeamService
{
    public List<TeamResponseDTO> Teams { get; } = new();

    public Task<List<TeamResponseDTO>> GetAllAsync(Guid organizationId) => Task.FromResult(Teams);

    public Task CreateAsync(CreateTeamDTO dto, Guid organizationId) => throw new NotImplementedException();
    public Task<TeamResponseDTO?> GetByIdAsync(Guid teamId, Guid organizationId) => throw new NotImplementedException();
    public Task UpdateAsync(Guid teamId, UpdateTeamDTO dto, Guid organizationId) => throw new NotImplementedException();
    public Task DeleteAsync(Guid teamId, Guid organizationId) => throw new NotImplementedException();
    public Task AddMemberAsync(Guid teamId, Guid userId, Guid organizationId) => throw new NotImplementedException();
    public Task RemoveMemberAsync(Guid teamId, Guid userId, Guid organizationId) => throw new NotImplementedException();
}

class FakeUserService : IUserService
{
    public List<UserDTO> Users { get; } = new();

    public Task<IEnumerable<UserDTO>> GetAllUsersAsync(Guid organizationId, Guid callerId, string callerRole) => Task.FromResult<IEnumerable<UserDTO>>(Users);
    public Task<UserDTO?> GetUserByIdAsync(Guid id, Guid organizationId) => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task<UserDTO> CreateUserAsync(UserCreateDTO dto, Guid organizationId) => throw new NotImplementedException();
    public Task<bool> DeleteUserAsync(Guid id, Guid organizationId) => throw new NotImplementedException();
    public Task<UserDTO> UpdateUserAsync(Guid userId, UserUpdateDTO dto, Guid organizationId) => throw new NotImplementedException();
    public Task<UserDTO> UpdateMyAvatarAsync(Guid userId, string? avatarUrl) => throw new NotImplementedException();
}

file class FakeTeamRepository : ITeamRepository
{
    public Task CreateAsync(Team team) => throw new NotImplementedException();
    public Task<Team?> GetByIdAsync(Guid id, Guid organizationId) => throw new NotImplementedException();
    public Task<List<Team>> GetAllAsync(Guid organizationId) => throw new NotImplementedException();
    public Task<Team?> GetByUserIdAsync(Guid userId, Guid organizationId) => Task.FromResult<Team?>(null);
    public Task<List<Guid>> GetManagedMemberIdsAsync(Guid managerUserId, Guid organizationId) => Task.FromResult(new List<Guid>());
    public Task UpdateAsync(Team team) => throw new NotImplementedException();
    public Task DeleteAsync(Team team) => throw new NotImplementedException();
}

public class ReportDataServiceTests
{
    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options,
            new FakeTenantContext { IsActive = false });

    private static (ReportDataService Service, FakeAppFocusService AppFocus, FakeUserService Users, FakeTeamService Teams, AppDbContext Db)
        CreateService(string dbName)
    {
        var db = CreateDb(dbName);
        var appFocus = new FakeAppFocusService();
        var users = new FakeUserService();
        var teams = new FakeTeamService();
        var teamRepo = new FakeTeamRepository();
        var service = new ReportDataService(appFocus, teams, users, teamRepo, db, NullLogger<ReportDataService>.Instance);
        return (service, appFocus, users, teams, db);
    }

    [Fact]
    public async Task GetReportAsync_AggregatesProductiveNeutralUnproductiveSeconds()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var (service, appFocus, users, _, db) = CreateService(Guid.NewGuid().ToString());

        db.Organizations.Add(new Organization { Id = orgId, Name = "Acme" });
        await db.SaveChangesAsync();

        users.Users.Add(new UserDTO { Id = userId, FirstName = "Ada", LastName = "Lovelace" });

        var day = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        appFocus.Events.Add(new AppFocusEvent
        {
            UserId = userId,
            OrganizationId = orgId,
            ApplicationName = "vscode.exe",
            Category = ActivityCategory.Productive,
            StartTime = day,
            EndTime = day.AddSeconds(100),
            DurationSeconds = 100
        });
        appFocus.Events.Add(new AppFocusEvent
        {
            UserId = userId,
            OrganizationId = orgId,
            ApplicationName = "youtube.exe",
            Category = ActivityCategory.Unproductive,
            StartTime = day.AddSeconds(200),
            EndTime = day.AddSeconds(250),
            DurationSeconds = 50
        });

        var filter = new ReportFilterDTO
        {
            UserId = userId,
            // Janela alargada em +/-1 dia: o serviço interpreta StartDate/EndDate como
            // horário local e converte para UTC internamente, então um filtro de
            // "só hoje" seria sensível ao fuso horário da máquina rodando o teste.
            StartDate = day.Date.AddDays(-1),
            EndDate = day.Date.AddDays(1),
            StartTime = TimeSpan.Zero,
            EndTime = new TimeSpan(23, 59, 59)
        };

        var report = await service.GetReportAsync(filter, orgId, Guid.NewGuid(), "TenantAdmin");

        Assert.Equal(100, report.ProductiveSeconds);
        Assert.Equal(0, report.NeutralSeconds);
        Assert.Equal(50, report.UnproductiveSeconds);
        Assert.Equal(150, report.TotalSeconds);
        Assert.Equal("user", report.SubjectType);
        Assert.Equal("Ada Lovelace", report.SubjectName);
        Assert.Equal(2, report.Activities.Count);
    }

    [Fact]
    public async Task GetReportAsync_FiltersOutEventsFromOtherOrganizations()
    {
        var orgId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var (service, appFocus, users, _, db) = CreateService(Guid.NewGuid().ToString());

        db.Organizations.Add(new Organization { Id = orgId, Name = "Acme" });
        await db.SaveChangesAsync();

        users.Users.Add(new UserDTO { Id = userId, FirstName = "Ada", LastName = "Lovelace" });

        var day = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        appFocus.Events.Add(new AppFocusEvent
        {
            UserId = userId,
            OrganizationId = otherOrgId, // pertence a outra organização
            ApplicationName = "vscode.exe",
            Category = ActivityCategory.Productive,
            StartTime = day,
            EndTime = day.AddSeconds(100),
            DurationSeconds = 100
        });

        var filter = new ReportFilterDTO
        {
            UserId = userId,
            // Janela alargada em +/-1 dia: o serviço interpreta StartDate/EndDate como
            // horário local e converte para UTC internamente, então um filtro de
            // "só hoje" seria sensível ao fuso horário da máquina rodando o teste.
            StartDate = day.Date.AddDays(-1),
            EndDate = day.Date.AddDays(1),
            StartTime = TimeSpan.Zero,
            EndTime = new TimeSpan(23, 59, 59)
        };

        var report = await service.GetReportAsync(filter, orgId, Guid.NewGuid(), "TenantAdmin");

        Assert.Empty(report.Activities);
        Assert.Equal(0, report.TotalSeconds);
    }

    [Fact]
    public async Task GetReportAsync_StartDateAfterEndDate_Throws()
    {
        var orgId = Guid.NewGuid();
        var (service, _, _, _, db) = CreateService(Guid.NewGuid().ToString());
        db.Organizations.Add(new Organization { Id = orgId, Name = "Acme" });
        await db.SaveChangesAsync();

        var filter = new ReportFilterDTO
        {
            StartDate = new DateTime(2026, 8, 10),
            EndDate = new DateTime(2026, 8, 1),
            StartTime = TimeSpan.Zero,
            EndTime = TimeSpan.Zero
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetReportAsync(filter, orgId, Guid.NewGuid(), "TenantAdmin"));
    }

    [Fact]
    public async Task GetReportAsync_UnknownOrganization_Throws()
    {
        var (service, _, _, _, _) = CreateService(Guid.NewGuid().ToString());

        var filter = new ReportFilterDTO
        {
            StartDate = new DateTime(2026, 8, 1),
            EndDate = new DateTime(2026, 8, 2),
            StartTime = TimeSpan.Zero,
            EndTime = new TimeSpan(23, 59, 59)
        };

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetReportAsync(filter, Guid.NewGuid(), Guid.NewGuid(), "TenantAdmin"));
    }
}
