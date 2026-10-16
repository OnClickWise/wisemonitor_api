using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.Helpers;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Models.Enums;
using WiseMonitor.Api.Services;
using Xunit;

namespace WiseMonitor.Api.Tests.Services;

file class NoopJwtService : IJwtService
{
    public string GenerateToken(User user) => "token";
    public string GenerateToken(User user, Guid orgId) => "token";
    public ClaimsPrincipal? ValidateToken(string token) => null;
    public Guid? GetUserIdFromToken(string token) => null;
}

file class NoopLiveSessionService : ILiveSessionService
{
    public Task<string> GetOrCreateSessionForOrganizationAsync(Guid organizationId) => Task.FromResult("s");
    public Task<string> GetOrCreateSessionForUserAsync(Guid organizationId, Guid userId) => Task.FromResult("s");
    public Task EndSessionForOrganizationAsync(Guid organizationId) => Task.CompletedTask;
    public Task EndSessionForUserAsync(Guid organizationId, Guid userId) => Task.CompletedTask;
    public bool HasActiveSession(Guid organizationId) => false;
    public bool HasActiveSession(Guid organizationId, Guid userId) => false;
}

file class NoopEmailService : IEmailService
{
    public Task SendEmailAsync(string to, string subject, string htmlMessage) => Task.CompletedTask;
}

/// <summary>
/// Regressões dos achados da auditoria de segurança — cada teste trava um buraco
/// que existia: se algum voltar a falhar, a correção foi desfeita.
/// </summary>
public class SecurityHardeningTests
{
    private const string Secret = "chave-de-teste-com-mais-de-32-bytes-para-hmac-sha256";

    private static IConfiguration Config() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = Secret,
                ["Jwt:Issuer"] = "WiseMonitorAuth",
                ["Jwt:Audience"] = "WiseMonitorUsers",
            })
            .Build();

    private static AppDbContext Db() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AuthService Auth(AppDbContext db) =>
        new(db, Config(), new NoopJwtService(), new NoopLiveSessionService(), new NoopEmailService(),
            new RefreshTokenService(db, Config()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthService>.Instance);

    private static User NewUser(Guid? orgId, string role = UserRoles.Employee, bool active = true) => new()
    {
        Id = Guid.NewGuid(),
        FirstName = "Ana",
        LastName = "Silva",
        Email = $"{Guid.NewGuid():N}@empresa.com",
        Role = role,
        IsActive = active,
        OrganizationId = orgId,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Senha@123"),
    };

    // ─── Escalonamento de papéis ───────────────────────────────

    [Theory]
    [InlineData(UserRoles.TenantAdmin, UserRoles.SuperAdmin, false)] // tenant nunca cria SuperAdmin
    [InlineData(UserRoles.HR, UserRoles.SuperAdmin, false)]
    [InlineData(UserRoles.HR, UserRoles.TenantAdmin, false)]          // RH não cria admin
    [InlineData(UserRoles.HR, UserRoles.HR, false)]                   // nem par do mesmo nível
    [InlineData(UserRoles.HR, UserRoles.Employee, true)]
    [InlineData(UserRoles.Manager, UserRoles.Director, false)]
    [InlineData(UserRoles.TenantAdmin, UserRoles.TenantAdmin, true)]
    [InlineData(UserRoles.TenantAdmin, UserRoles.Employee, true)]
    [InlineData(UserRoles.TenantAdmin, "papel-inexistente", true)]    // Normalize → Employee
    public void CanAssign_EnforcesHierarchy(string caller, string target, bool expected) =>
        Assert.Equal(expected, UserRoles.CanAssign(caller, target));

    [Theory]
    [InlineData(UserRoles.HR, UserRoles.TenantAdmin, false)] // RH não edita e-mail do admin
    [InlineData(UserRoles.HR, UserRoles.Employee, true)]
    [InlineData(UserRoles.TenantAdmin, UserRoles.SuperAdmin, false)]
    [InlineData(UserRoles.TenantAdmin, UserRoles.Director, true)]
    public void CanManage_EnforcesHierarchy(string caller, string targetCurrent, bool expected) =>
        Assert.Equal(expected, UserRoles.CanManage(caller, targetCurrent));

    [Fact]
    public void JwtService_RoleSuperAdminWithoutFlag_IsDowngraded()
    {
        var user = NewUser(Guid.NewGuid(), role: UserRoles.SuperAdmin);
        user.IsSuperAdmin = false;

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(new JwtService(Config()).GenerateToken(user));

        Assert.DoesNotContain(jwt.Claims, c => c.Value == UserRoles.SuperAdmin);
        Assert.Contains(jwt.Claims, c => c.Type == "isSuperAdmin" && c.Value == "false");
    }

    [Fact]
    public void JwtService_RealSuperAdmin_KeepsRole()
    {
        var user = NewUser(null, role: UserRoles.SuperAdmin);
        user.IsSuperAdmin = true;

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(new JwtService(Config()).GenerateToken(user));

        Assert.Contains(jwt.Claims, c => c.Type == "role" && c.Value == UserRoles.SuperAdmin);
    }

    // ─── URLs assinadas de captura ─────────────────────────────

    private static ScreenshotService Screens() => new(null!, Config());

    private static (Guid Id, long Exp, string Sig) ParseSigned(string path)
    {
        var uri = new Uri("https://x" + path);
        var id = Guid.Parse(uri.AbsolutePath.Split('/').Last());
        var q = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
        return (id, long.Parse(q["exp"]!), q["sig"]!);
    }

    [Fact]
    public void ScreenshotSignedUrl_Valid_IsAccepted()
    {
        var svc = Screens();
        var (id, exp, sig) = ParseSigned(svc.BuildSignedPath(Guid.NewGuid()));
        Assert.True(svc.ValidateAccess(id, exp, sig));
    }

    [Fact]
    public void ScreenshotSignedUrl_WithoutSignature_IsRejected() =>
        Assert.False(Screens().ValidateAccess(Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), null));

    [Fact]
    public void ScreenshotSignedUrl_ForOtherId_IsRejected()
    {
        var svc = Screens();
        var (_, exp, sig) = ParseSigned(svc.BuildSignedPath(Guid.NewGuid()));
        Assert.False(svc.ValidateAccess(Guid.NewGuid(), exp, sig));
    }

    [Fact]
    public void ScreenshotSignedUrl_ExtendedExpiry_IsRejected()
    {
        var svc = Screens();
        var (id, exp, sig) = ParseSigned(svc.BuildSignedPath(Guid.NewGuid()));
        Assert.False(svc.ValidateAccess(id, exp + 86400, sig));
    }

    [Fact]
    public void ScreenshotSignedUrl_Expired_IsRejected()
    {
        // Assinatura correta, mas com prazo no passado.
        var svc = Screens();
        var id = Guid.NewGuid();
        var exp = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds();
        var hash = System.Security.Cryptography.HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes($"screenshot|{id}|{exp}"));
        var sig = Convert.ToBase64String(hash).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        Assert.False(svc.ValidateAccess(id, exp, sig));
    }

    // ─── Login ─────────────────────────────────────────────────

    [Fact]
    public async Task LoginByEmail_InactiveUser_IsRejected()
    {
        using var db = Db();
        var user = NewUser(null, active: false);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Auth(db).LoginByEmailAsync(user.Email, "Senha@123"));
    }

    [Fact]
    public async Task LoginByEmail_SuspendedOrganization_IsRejected()
    {
        using var db = Db();
        var org = new Organization { Name = "Acme", Status = "Suspended" };
        db.Organizations.Add(org);
        var user = NewUser(org.Id);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Auth(db).LoginByEmailAsync(user.Email, "Senha@123"));
    }

    [Fact]
    public async Task ResetPassword_RevokesRefreshTokens()
    {
        using var db = Db();
        var user = NewUser(null);
        db.Users.Add(user);
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(), UserId = user.Id, Token = "reset-token", Expiration = DateTime.UtcNow.AddHours(1)
        });
        await db.SaveChangesAsync();

        var auth = Auth(db);
        var refresh = await new RefreshTokenService(db, Config()).IssueAsync(user.Id);

        await auth.ResetPasswordAsync("reset-token", "NovaSenha@456");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => auth.RefreshAsync(refresh));
    }

    [Fact]
    public void LoginAttemptTracker_LocksAfterMaxFailures_AndExpires()
    {
        var now = DateTime.UtcNow;
        var tracker = new LoginAttemptTracker(() => now);

        for (var i = 0; i < LoginAttemptTracker.MaxFailures - 1; i++)
            tracker.RecordFailure("Ana@Empresa.com");
        Assert.False(tracker.IsLockedOut("ana@empresa.com", out _));

        tracker.RecordFailure("ana@empresa.com");
        Assert.True(tracker.IsLockedOut("ANA@empresa.com", out var retry));
        Assert.True(retry > TimeSpan.Zero);

        now += LoginAttemptTracker.LockoutDuration + TimeSpan.FromSeconds(1);
        Assert.False(tracker.IsLockedOut("ana@empresa.com", out _));
    }

    [Fact]
    public void LoginAttemptTracker_SuccessResetsCounter()
    {
        var tracker = new LoginAttemptTracker();
        for (var i = 0; i < LoginAttemptTracker.MaxFailures - 1; i++)
            tracker.RecordFailure("ana@empresa.com");

        tracker.Reset("ana@empresa.com");
        tracker.RecordFailure("ana@empresa.com");

        Assert.False(tracker.IsLockedOut("ana@empresa.com", out _));
    }

    // ─── Uploads de imagem ─────────────────────────────────────

    private static IFormFile File(byte[] content, string name) =>
        new FormFile(new MemoryStream(content), 0, content.Length, "file", name);

    [Fact]
    public async Task ImageUpload_HtmlDisguisedAsPng_IsRejected()
    {
        var result = await ImageUploadValidator.ValidateAsync(
            File(Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>"), "avatar.png"));
        Assert.False(result.Ok);
    }

    [Fact]
    public async Task ImageUpload_Svg_IsRejected()
    {
        var result = await ImageUploadValidator.ValidateAsync(
            File(Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" onload=\"alert(1)\"/>"), "logo.svg"));
        Assert.False(result.Ok);
    }

    [Fact]
    public async Task ImageUpload_RealPng_UsesCanonicalExtension()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D };
        var result = await ImageUploadValidator.ValidateAsync(File(png, "foto.html"));
        Assert.True(result.Ok);
        Assert.Equal(".png", result.Extension);
    }

    [Fact]
    public async Task ImageUpload_TooLarge_IsRejected()
    {
        var big = new byte[ImageUploadValidator.MaxBytes + 1];
        big[0] = 0xFF; big[1] = 0xD8; big[2] = 0xFF;
        Assert.False((await ImageUploadValidator.ValidateAsync(File(big, "a.jpg"))).Ok);
    }

    [Theory]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:image/svg+xml;base64,AAAA", false)]
    [InlineData("http://inseguro.com/logo.png", false)]
    [InlineData("/uploads/../appsettings.json", false)]
    [InlineData("https://cdn.empresa.com/logo.png", true)]
    [InlineData("/uploads/logos/abc.png", true)]
    [InlineData("", true)]
    public void ImageUrl_OnlyHttpsOrOwnUploads(string url, bool expected) =>
        Assert.Equal(expected, ImageUploadValidator.IsSafeImageUrl(url));
}
