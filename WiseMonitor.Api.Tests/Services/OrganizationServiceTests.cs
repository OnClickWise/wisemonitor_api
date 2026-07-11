using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Services;
using Xunit;

namespace WiseMonitor.Api.Tests.Services;

public class OrganizationServiceTests
{
    private static AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(opts);
    }

    private static RegisterOrganizationDTO ValidDto(
        string orgName  = "Empresa Teste",
        string email    = "admin@empresa.com",
        string password = "Senha@1234") => new()
    {
        OrganizationName = orgName,
        AdminFirstName   = "Maria",
        AdminLastName    = "Silva",
        AdminEmail       = email,
        AdminPassword    = password
    };

    // ─── Registro com sucesso ───────────────────────────────────

    [Fact]
    public async Task Register_ValidData_ReturnsSuccess()
    {
        var svc    = new OrganizationService(CreateDb());
        var result = await svc.RegisterOrganizationAsync(ValidDto());

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task Register_ValidData_PersistsOrgAndUser()
    {
        var db  = CreateDb();
        var svc = new OrganizationService(db);

        await svc.RegisterOrganizationAsync(ValidDto("Empresa ABC", "admin@abc.com"));

        Assert.Equal(1, await db.Organizations.CountAsync());
        Assert.Equal(1, await db.Users.CountAsync());

        var user = await db.Users.FirstAsync();
        Assert.Equal("admin@abc.com", user.Email);
        Assert.Equal("admin", user.Role);
        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task Register_ValidData_PasswordIsHashed()
    {
        var db  = CreateDb();
        var svc = new OrganizationService(db);

        await svc.RegisterOrganizationAsync(ValidDto());

        var user = await db.Users.FirstAsync();
        Assert.NotEqual("Senha@1234", user.PasswordHash);
        Assert.StartsWith("$2", user.PasswordHash);
    }

    // ─── Duplicatas bloqueadas ──────────────────────────────────

    [Fact]
    public async Task Register_DuplicateOrgName_ReturnsFail()
    {
        var db  = CreateDb();
        var svc = new OrganizationService(db);

        await svc.RegisterOrganizationAsync(ValidDto("Empresa Duplicada", "admin1@emp.com"));
        var result = await svc.RegisterOrganizationAsync(ValidDto("empresa duplicada", "admin2@emp.com")); // nome idêntico case-insensitive

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsFail()
    {
        var db  = CreateDb();
        var svc = new OrganizationService(db);

        await svc.RegisterOrganizationAsync(ValidDto("Empresa A", "admin@empresa.com"));
        var result = await svc.RegisterOrganizationAsync(ValidDto("Empresa B", "admin@empresa.com")); // mesmo email

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Register_DuplicateEmail_DoesNotPersistSecondOrg()
    {
        var db  = CreateDb();
        var svc = new OrganizationService(db);

        await svc.RegisterOrganizationAsync(ValidDto("Empresa A", "admin@empresa.com"));
        await svc.RegisterOrganizationAsync(ValidDto("Empresa B", "admin@empresa.com"));

        Assert.Equal(1, await db.Organizations.CountAsync());
        Assert.Equal(1, await db.Users.CountAsync());
    }

    // ─── Normalização de dados ──────────────────────────────────

    [Fact]
    public async Task Register_EmailIsNormalized_ToLowercase()
    {
        var db  = CreateDb();
        var svc = new OrganizationService(db);

        await svc.RegisterOrganizationAsync(ValidDto(email: "Admin@EMPRESA.COM"));

        var user = await db.Users.FirstAsync();
        Assert.Equal("admin@empresa.com", user.Email);
    }

    [Fact]
    public async Task Register_OrgNameIsTrimmed()
    {
        var db  = CreateDb();
        var svc = new OrganizationService(db);

        await svc.RegisterOrganizationAsync(ValidDto(orgName: "  Empresa Teste  "));

        var org = await db.Organizations.FirstAsync();
        Assert.Equal("Empresa Teste", org.Name);
    }
}
