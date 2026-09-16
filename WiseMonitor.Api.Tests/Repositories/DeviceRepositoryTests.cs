using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Repositories;
using WiseMonitor.Api.Services;
using Xunit;

namespace WiseMonitor.Api.Tests.Repositories;

file class FakeTenantContext : ITenantContext
{
    public Guid? OrganizationId { get; init; }
    public bool IsSuperAdmin { get; init; }
    public bool IsActive { get; init; }
}

public class DeviceRepositoryTests
{
    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options,
            new FakeTenantContext { IsActive = false });

    [Fact]
    public async Task GetByHostnameAsync_IsCaseInsensitive()
    {
        var db = CreateDb(Guid.NewGuid().ToString());
        var orgId = Guid.NewGuid();
        var repo = new DeviceRepository(db);

        db.Devices.Add(new Device { Hostname = "DESKTOP-ABC123", OrganizationId = orgId });
        await db.SaveChangesAsync();

        var found = await repo.GetByHostnameAsync("desktop-abc123", orgId);

        Assert.NotNull(found);
    }

    // MarkStaleOfflineAsync uses ExecuteUpdateAsync, which the EF Core InMemory
    // provider doesn't support (SQL-only bulk update) — verify it against a real
    // Postgres instance instead (see Phase 3 migration/manual verification notes).
}
