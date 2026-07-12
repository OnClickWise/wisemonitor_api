using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Helpers;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Services;
using Xunit;

namespace WiseMonitor.Api.Tests.Services;

file class FakeJwtService : IJwtService
{
    public string GenerateToken(User user) => "fake-token";
    public string GenerateToken(User user, Guid orgId) => "fake-token";
    public ClaimsPrincipal? ValidateToken(string token) => null;
    public Guid? GetUserIdFromToken(string token) => null;
}

file class FakeLiveSessionService : ILiveSessionService
{
    public Task<string> GetOrCreateSessionForOrganizationAsync(Guid organizationId) => Task.FromResult("fake-session");
    public Task<string> GetOrCreateSessionForUserAsync(Guid organizationId, Guid userId) => Task.FromResult("fake-session");
    public Task EndSessionForOrganizationAsync(Guid organizationId) => Task.CompletedTask;
    public Task EndSessionForUserAsync(Guid organizationId, Guid userId) => Task.CompletedTask;
    public bool HasActiveSession(Guid organizationId) => false;
    public bool HasActiveSession(Guid organizationId, Guid userId) => false;
}

file class FakeEmailService : IEmailService
{
    public Task SendEmailAsync(string to, string subject, string htmlMessage) => Task.CompletedTask;
}

public class AuthServiceTests
{
    private static AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    private static IConfiguration CreateConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"] = "super-secret-key-for-tests-must-be-long-enough-32chars",
                ["JwtSettings:Issuer"]    = "TestIssuer",
                ["JwtSettings:Audience"]  = "TestAudience"
            })
            .Build();

    private static AuthService CreateService(AppDbContext db) =>
        new AuthService(db, CreateConfig(), new FakeJwtService(), new FakeLiveSessionService(), new FakeEmailService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthService>.Instance);

    // ─── HashPassword ──────────────────────────────────────────

    [Fact]
    public void HashPassword_ValidInput_ReturnsBcryptHash()
    {
        var svc  = CreateService(CreateDb());
        var hash = svc.HashPassword("MinhaS3nha!");
        Assert.NotNull(hash);
        Assert.StartsWith("$2", hash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void HashPassword_EmptyOrWhitespace_Throws(string password)
    {
        var svc = CreateService(CreateDb());
        Assert.Throws<ArgumentException>(() => svc.HashPassword(password));
    }

    // ─── VerifyPassword ────────────────────────────────────────

    [Fact]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        var svc  = CreateService(CreateDb());
        var hash = svc.HashPassword("Senha@123");
        Assert.True(svc.VerifyPassword("Senha@123", hash));
    }

    [Fact]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        var svc  = CreateService(CreateDb());
        var hash = svc.HashPassword("Senha@123");
        Assert.False(svc.VerifyPassword("OutraSenha", hash));
    }

    [Theory]
    [InlineData(null, "some-hash")]
    [InlineData("password", null)]
    [InlineData(null, null)]
    public void VerifyPassword_NullInputs_ReturnsFalse(string? pwd, string? hash)
    {
        var svc = CreateService(CreateDb());
        Assert.False(svc.VerifyPassword(pwd, hash));
    }

    // ─── LoginAsync ────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsTokenAndUserData()
    {
        var db  = CreateDb();
        var svc = CreateService(db);

        var orgId = Guid.NewGuid();
        db.Organizations.Add(new Organization { Id = orgId, Name = "TestOrg" });
        db.Users.Add(new User
        {
            Id           = Guid.NewGuid(),
            Email        = "joao@empresa.com",
            PasswordHash = svc.HashPassword("Senha@123"),
            Role         = "admin",
            IsActive     = true,
            OrganizationId = orgId,
            FirstName    = "João",
            LastName     = "Silva"
        });
        await db.SaveChangesAsync();

        var result = await svc.LoginAsync(new LoginRequestDTO
        {
            Email          = "joao@empresa.com",
            Password       = "Senha@123",
            OrganizationId = orgId
        });

        Assert.NotNull(result.Token);
        Assert.NotEmpty(result.Token);
        Assert.Equal("João Silva", result.FullName);
        Assert.Equal("admin", result.Role);
        Assert.Equal(orgId, result.OrganizationId);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsUnauthorized()
    {
        var db  = CreateDb();
        var svc = CreateService(db);

        var orgId = Guid.NewGuid();
        db.Organizations.Add(new Organization { Id = orgId, Name = "TestOrg" });
        db.Users.Add(new User
        {
            Email        = "joao@empresa.com",
            PasswordHash = svc.HashPassword("Senha@123"),
            IsActive     = true,
            OrganizationId = orgId,
            FirstName = "J", LastName = "S"
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.LoginAsync(new LoginRequestDTO
            {
                Email          = "joao@empresa.com",
                Password       = "SenhaErrada",
                OrganizationId = orgId
            }));
    }

    [Fact]
    public async Task LoginAsync_InactiveUser_ThrowsUnauthorized()
    {
        var db  = CreateDb();
        var svc = CreateService(db);

        var orgId = Guid.NewGuid();
        db.Organizations.Add(new Organization { Id = orgId, Name = "TestOrg" });
        db.Users.Add(new User
        {
            Email        = "inativo@empresa.com",
            PasswordHash = svc.HashPassword("Senha@123"),
            IsActive     = false,
            OrganizationId = orgId,
            FirstName = "J", LastName = "S"
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.LoginAsync(new LoginRequestDTO
            {
                Email          = "inativo@empresa.com",
                Password       = "Senha@123",
                OrganizationId = orgId
            }));
    }

    [Fact]
    public async Task LoginAsync_UserNotFound_ThrowsUnauthorized()
    {
        var svc = CreateService(CreateDb());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.LoginAsync(new LoginRequestDTO
            {
                Email          = "naoexiste@empresa.com",
                Password       = "Senha@123",
                OrganizationId = Guid.NewGuid()
            }));
    }

    [Fact]
    public async Task LoginAsync_NullDto_ThrowsArgumentNull()
    {
        var svc = CreateService(CreateDb());
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            svc.LoginAsync(null!));
    }

    // ─── LoginByEmailAsync (caminho real usado pelo AuthController) ────

    [Fact]
    public async Task LoginByEmailAsync_ValidCredentials_ReturnsTokenAndUserData()
    {
        var db = CreateDb();
        var svc = CreateService(db);

        var orgId = Guid.NewGuid();
        db.Organizations.Add(new Organization { Id = orgId, Name = "TestOrg" });
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "maria@empresa.com",
            PasswordHash = svc.HashPassword("Senha@123"),
            Role = "admin",
            IsActive = true,
            OrganizationId = orgId,
            FirstName = "Maria",
            LastName = "Souza"
        });
        await db.SaveChangesAsync();

        var result = await svc.LoginByEmailAsync("maria@empresa.com", "Senha@123");

        Assert.NotNull(result.Token);
        Assert.NotEmpty(result.SessionId);
        Assert.Equal(orgId, result.OrganizationId);
        Assert.Equal("maria@empresa.com", result.User.Email);
    }

    [Fact]
    public async Task LoginByEmailAsync_WrongPassword_ThrowsUnauthorized()
    {
        var db = CreateDb();
        var svc = CreateService(db);

        db.Users.Add(new User
        {
            Email = "maria@empresa.com",
            PasswordHash = svc.HashPassword("Senha@123"),
            IsActive = true,
            FirstName = "M",
            LastName = "S"
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.LoginByEmailAsync("maria@empresa.com", "SenhaErrada"));
    }

    [Fact]
    public async Task LoginByEmailAsync_UserNotFound_ThrowsUnauthorized()
    {
        var svc = CreateService(CreateDb());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.LoginByEmailAsync("naoexiste@empresa.com", "Senha@123"));
    }
}
