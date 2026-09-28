using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Repositories;
using WiseMonitor.Api.Services;
using Xunit;

namespace WiseMonitor.Api.Tests.Services;

public class UserServiceTests
{
    // CreateUserAsync não usa o ITeamRepository — stub vazio evita puxar mock library.
    private class NoopTeamRepository : ITeamRepository
    {
        public Task CreateAsync(Team team) => Task.CompletedTask;
        public Task<Team?> GetByIdAsync(Guid id, Guid organizationId) => Task.FromResult<Team?>(null);
        public Task<List<Team>> GetAllAsync(Guid organizationId) => Task.FromResult(new List<Team>());
        public Task<Team?> GetByUserIdAsync(Guid userId, Guid organizationId) => Task.FromResult<Team?>(null);
        public Task<List<Guid>> GetManagedMemberIdsAsync(Guid managerUserId, Guid organizationId) => Task.FromResult(new List<Guid>());
        public Task UpdateAsync(Team team) => Task.CompletedTask;
        public Task DeleteAsync(Team team) => Task.CompletedTask;
    }

    private static AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(opts);
    }

    private static UserService CreateService(AppDbContext db) => new(db, new NoopTeamRepository());

    private static UserCreateDTO ValidDto(
        string firstName = "João",
        string lastName  = "Souza",
        string email     = "joao@empresa.com") => new()
    {
        FirstName = firstName,
        LastName  = lastName,
        Email     = email,
        Password  = "Senha@1234",
        Role      = "employee",
        IsActive  = true
    };

    // ─── Criação com sucesso ────────────────────────────────────

    [Fact]
    public async Task CreateUser_ValidData_PersistsUser()
    {
        var db  = CreateDb();
        var svc = CreateService(db);
        var orgId = Guid.NewGuid();

        var created = await svc.CreateUserAsync(ValidDto(), orgId);

        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal("joao@empresa.com", created.Email);
    }

    // ─── Duplicidade bloqueada ──────────────────────────────────

    [Fact]
    public async Task CreateUser_DuplicateEmail_ThrowsAndDoesNotPersist()
    {
        var db  = CreateDb();
        var svc = CreateService(db);
        var orgId = Guid.NewGuid();

        await svc.CreateUserAsync(ValidDto(email: "joao@empresa.com"), orgId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateUserAsync(ValidDto(firstName: "Outro", lastName: "Nome", email: "JOAO@empresa.com"), orgId));

        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_AcrossDifferentOrganizations_ThrowsToo()
    {
        var db  = CreateDb();
        var svc = CreateService(db);

        await svc.CreateUserAsync(ValidDto(email: "joao@empresa.com"), Guid.NewGuid());

        // E-mail é identificador de login — mesma regra do cadastro de organização
        // (RegisterOrganizationAsync), que bloqueia globalmente, não só por org.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateUserAsync(ValidDto(firstName: "Outro", lastName: "Nome", email: "joao@empresa.com"), Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateUser_DuplicateNameInSameOrg_ThrowsAndDoesNotPersist()
    {
        var db  = CreateDb();
        var svc = CreateService(db);
        var orgId = Guid.NewGuid();

        await svc.CreateUserAsync(ValidDto(email: "joao1@empresa.com"), orgId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateUserAsync(ValidDto(firstName: "joão", lastName: "souza", email: "joao2@empresa.com"), orgId));

        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task CreateUser_SameNameInDifferentOrgs_IsAllowed()
    {
        var db  = CreateDb();
        var svc = CreateService(db);

        await svc.CreateUserAsync(ValidDto(email: "joao1@empresa.com"), Guid.NewGuid());
        await svc.CreateUserAsync(ValidDto(email: "joao2@outraempresa.com"), Guid.NewGuid());

        Assert.Equal(2, await db.Users.CountAsync());
    }

    // ─── Normalização ───────────────────────────────────────────

    [Fact]
    public async Task CreateUser_EmailIsNormalized_ToLowercaseAndTrimmed()
    {
        var db  = CreateDb();
        var svc = CreateService(db);

        await svc.CreateUserAsync(ValidDto(email: "  Joao@EMPRESA.com  "), Guid.NewGuid());

        var user = await db.Users.FirstAsync();
        Assert.Equal("joao@empresa.com", user.Email);
    }
}
