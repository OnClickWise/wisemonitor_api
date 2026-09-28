using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Models.Billing;
using WiseMonitor.Api.Services;
using Xunit;

namespace WiseMonitor.Api.Tests.Services;

public class PlanCatalogTests
{
    [Theory]
    [InlineData("Free", PlanCodes.Starter)]
    [InlineData("Basic", PlanCodes.Starter)]
    [InlineData("Pro", PlanCodes.Professional)]
    [InlineData("business", PlanCodes.Business)]
    [InlineData(null, PlanCodes.Starter)]
    public void Normalize_MapsLegacyNames(string? input, string expected) =>
        Assert.Equal(expected, PlanCatalog.Normalize(input));

    [Fact]
    public void HigherPlans_IncludeAllLowerPlanFeatures()
    {
        var ordered = PlanCatalog.Plans.OrderBy(p => p.Rank).ToList();
        for (var i = 1; i < ordered.Count; i++)
            Assert.True(ordered[i - 1].Features.IsSubsetOf(ordered[i].Features));
    }

    [Fact]
    public void Starter_DoesNotIncludeScreenshots_ProfessionalDoes()
    {
        Assert.DoesNotContain(Features.Screenshots, PlanCatalog.Get(PlanCodes.Starter).Features);
        Assert.Contains(Features.Screenshots, PlanCatalog.Get(PlanCodes.Professional).Features);
        Assert.Equal(PlanCodes.Business, PlanCatalog.LowestPlanWith(Features.Departments));
        Assert.Equal(PlanCodes.Enterprise, PlanCatalog.LowestPlanWith(Features.Sso));
    }

    [Theory]
    [InlineData(10, 0)]
    [InlineData(11, 5)]
    [InlineData(50, 5)]
    [InlineData(100, 10)]
    [InlineData(101, 15)]
    [InlineData(500, 20)]
    [InlineData(1000, 25)]
    [InlineData(5000, 25)]
    public void VolumeDiscount_FollowsTiers(int seats, decimal expected) =>
        Assert.Equal(expected, PlanCatalog.VolumeDiscountFor(seats));

    [Fact]
    public void Quote_100ProfessionalSeats_Applies10Percent()
    {
        var q = PlanCatalog.Quote(PlanCodes.Professional, 100, BillingCycles.Monthly);
        Assert.Equal(590m, q.ListPriceUsd);
        Assert.Equal(531m, q.TotalUsd);
        Assert.False(q.RequiresSales);
    }

    [Fact]
    public void Quote_50ProfessionalSeats_Applies5Percent()
    {
        var q = PlanCatalog.Quote(PlanCodes.Professional, 50, BillingCycles.Monthly);
        Assert.Equal(280.25m, q.TotalUsd);
    }

    [Fact]
    public void Quote_SmallStarter_AppliesOrganizationMinimum()
    {
        var q = PlanCatalog.Quote(PlanCodes.Starter, 2, BillingCycles.Monthly);
        Assert.True(q.MinimumApplied);
        Assert.Equal(10m, q.TotalUsd);

        var above = PlanCatalog.Quote(PlanCodes.Starter, 3, BillingCycles.Monthly);
        Assert.False(above.MinimumApplied);
        Assert.Equal(11.70m, above.TotalUsd);
    }

    [Fact]
    public void Quote_EnterpriseOrOver1000Seats_RequiresSales()
    {
        Assert.True(PlanCatalog.Quote(PlanCodes.Enterprise, 20, BillingCycles.Monthly).RequiresSales);
        Assert.True(PlanCatalog.Quote(PlanCodes.Business, 1001, BillingCycles.Monthly).RequiresSales);
    }
}

public class EntitlementServiceTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static async Task<Organization> SeedOrg(AppDbContext db, string plan, string status = "Active",
        DateTime? trialEndsAt = null, int? maxUsers = null, int users = 0)
    {
        var org = new Organization { Name = "Org", Plan = plan, Status = status, TrialEndsAt = trialEndsAt, MaxUsers = maxUsers };
        db.Organizations.Add(org);
        for (var i = 0; i < users; i++)
            db.Users.Add(new User { FirstName = "U", LastName = $"{i}", Email = $"u{i}@x.com", OrganizationId = org.Id, IsActive = true });
        await db.SaveChangesAsync();
        return org;
    }

    [Fact]
    public async Task Starter_HasNoScreenshots_AndThirtyDayRetention()
    {
        var db = CreateDb();
        var org = await SeedOrg(db, PlanCodes.Starter);

        var snap = await new EntitlementService(db).GetAsync(org.Id);

        Assert.NotNull(snap);
        Assert.False(snap!.Has(Features.Screenshots));
        Assert.True(snap.Has(Features.TimeTracking));
        Assert.Equal(30, snap.RetentionDays);
    }

    [Fact]
    public async Task ActiveTrial_GetsProfessional_WithoutSeatLimit()
    {
        var db = CreateDb();
        var org = await SeedOrg(db, PlanCodes.Professional, "Trial", DateTime.UtcNow.AddDays(5), maxUsers: 1, users: 3);

        var snap = (await new EntitlementService(db).GetAsync(org.Id))!;

        Assert.True(snap.IsTrial);
        Assert.False(snap.TrialExpired);
        Assert.Equal(5, snap.TrialDaysRemaining);
        Assert.True(snap.Has(Features.Screenshots));
        Assert.True(snap.HasSeatAvailable);
    }

    [Fact]
    public async Task ExpiredTrial_FallsBackToStarterFeatures()
    {
        var db = CreateDb();
        var org = await SeedOrg(db, PlanCodes.Professional, "Trial", DateTime.UtcNow.AddMinutes(-1));

        var snap = (await new EntitlementService(db).GetAsync(org.Id))!;

        Assert.True(snap.TrialExpired);
        Assert.Equal(0, snap.TrialDaysRemaining);
        Assert.False(snap.Has(Features.Screenshots));
        Assert.True(snap.Has(Features.TimeTracking));
    }

    [Fact]
    public async Task AddOns_GrantFeatures_AndExtendRetention()
    {
        var db = CreateDb();
        var org = await SeedOrg(db, PlanCodes.Business);
        db.OrganizationAddOns.AddRange(
            new OrganizationAddOn { OrganizationId = org.Id, Code = AddOnCodes.AiInsights },
            new OrganizationAddOn { OrganizationId = org.Id, Code = AddOnCodes.ExtendedStorage, Quantity = 90 },
            new OrganizationAddOn { OrganizationId = org.Id, Code = AddOnCodes.Dlp, EndsAt = DateTime.UtcNow.AddDays(-1) });
        await db.SaveChangesAsync();

        var snap = (await new EntitlementService(db).GetAsync(org.Id))!;

        Assert.True(snap.Has(Features.AiInsights));
        Assert.False(snap.Has(Features.Dlp)); // add-on expirado
        Assert.Equal(270, snap.RetentionDays);
    }

    [Fact]
    public async Task PaidPlan_EnforcesPurchasedSeats()
    {
        var db = CreateDb();
        var org = await SeedOrg(db, PlanCodes.Professional, maxUsers: 2, users: 2);

        var snap = (await new EntitlementService(db).GetAsync(org.Id))!;

        Assert.Equal(2, snap.SeatsUsed);
        Assert.False(snap.HasSeatAvailable);
    }

    [Fact]
    public async Task Register_StartsProfessionalTrial()
    {
        var db = CreateDb();
        var result = await new OrganizationService(db).RegisterOrganizationAsync(new RegisterOrganizationDTO
        {
            OrganizationName = "Nova Org",
            AdminFirstName = "Ana",
            AdminLastName = "Silva",
            AdminEmail = "ana@nova.com",
            AdminPassword = "Senha@1234",
        });

        Assert.True(result.Success);
        var org = await db.Organizations.SingleAsync();
        Assert.Equal(PlanCodes.Professional, org.Plan);
        Assert.Equal("Trial", org.Status);
        Assert.InRange(org.TrialEndsAt!.Value, DateTime.UtcNow.AddDays(13.9), DateTime.UtcNow.AddDays(14.1));
    }
}
