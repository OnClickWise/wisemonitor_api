using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Services;
using Xunit;

namespace WiseMonitor.Api.Tests.Data;

file class FakeTenantContext : ITenantContext
{
    public Guid? OrganizationId { get; init; }
    public bool IsSuperAdmin { get; init; }
    public bool IsActive { get; init; } = true;
}

public class TenantIsolationTests
{
    private static AppDbContext CreateDb(string name, ITenantContext tenant)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(opts, tenant);
    }

    [Fact]
    public async Task Devices_UserFromOrgB_CannotSeeDeviceFromOrgA()
    {
        var dbName = Guid.NewGuid().ToString();
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();

        // Seed com tenant desativado (sem filtro) para poder inserir dados de ambas as orgs.
        using (var seedDb = CreateDb(dbName, new FakeTenantContext { IsActive = false }))
        {
            seedDb.Devices.Add(new Device { Hostname = "PC-ORG-A", OrganizationId = orgA });
            seedDb.Devices.Add(new Device { Hostname = "PC-ORG-B", OrganizationId = orgB });
            await seedDb.SaveChangesAsync();
        }

        using var dbAsOrgB = CreateDb(dbName, new FakeTenantContext { OrganizationId = orgB, IsActive = true });
        var visibleDevices = await dbAsOrgB.Devices.ToListAsync();

        Assert.Single(visibleDevices);
        Assert.Equal("PC-ORG-B", visibleDevices[0].Hostname);
    }

    [Fact]
    public async Task Devices_SuperAdmin_SeesAllOrganizations()
    {
        var dbName = Guid.NewGuid().ToString();
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();

        using (var seedDb = CreateDb(dbName, new FakeTenantContext { IsActive = false }))
        {
            seedDb.Devices.Add(new Device { Hostname = "PC-ORG-A", OrganizationId = orgA });
            seedDb.Devices.Add(new Device { Hostname = "PC-ORG-B", OrganizationId = orgB });
            await seedDb.SaveChangesAsync();
        }

        using var dbAsSuperAdmin = CreateDb(dbName, new FakeTenantContext { IsSuperAdmin = true, IsActive = true });
        var visibleDevices = await dbAsSuperAdmin.Devices.ToListAsync();

        Assert.Equal(2, visibleDevices.Count);
    }
}
