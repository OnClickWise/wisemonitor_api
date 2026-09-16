using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Repositories;
using WiseMonitor.Api.Services;
using Xunit;

namespace WiseMonitor.Api.Tests.Services;

file class FakeTenantContext : ITenantContext
{
    public Guid? OrganizationId { get; init; }
    public bool IsSuperAdmin { get; init; }
    public bool IsActive { get; init; }
}

public class KeyboardMouseServiceTests
{
    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options,
            new FakeTenantContext { IsActive = false });

    [Fact]
    public async Task ProcessKeyboardEventAsync_PersistsWordsAndQualityMetrics()
    {
        var db = CreateDb(Guid.NewGuid().ToString());
        var service = new KeyboardService(new KeyboardRepository(db));
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var start = new DateTime(2026, 8, 7, 10, 0, 0, DateTimeKind.Utc);

        var dto = new KeyboardEventCreateDTO
        {
            SessionId = Guid.NewGuid(),
            StartAt = start,
            EndAt = start.AddMinutes(2),
            Application = "chrome.exe",
            Metrics = new KeyboardMetricsDTO
            {
                TotalKeystrokes = 100,
                Letters = 80,
                Words = 20,
                Numbers = 0,
                Symbols = 0,
                BackspaceCount = 10
            },
            Words = new List<KeyboardWordItemDTO>
            {
                new() { Word = "hello", Count = 3 },
                new() { Word = "world", Count = 1 }
            }
        };

        await service.ProcessKeyboardEventAsync(dto, userId, orgId);

        var session = await db.KeyboardSessions.Include(k => k.Words).FirstAsync();
        Assert.Equal(10, session.BackspaceCount);
        Assert.Equal(10.0, session.WordsPerMinute);
        Assert.Equal(0.1, session.CorrectionRate);
        Assert.Equal(2, session.Words.Count);
        Assert.Contains(session.Words, w => w.Word == "hello" && w.Count == 3);
    }

    [Fact]
    public async Task ProcessKeyboardEventAsync_NullMetrics_DoesNotThrow()
    {
        var db = CreateDb(Guid.NewGuid().ToString());
        var service = new KeyboardService(new KeyboardRepository(db));

        var dto = new KeyboardEventCreateDTO
        {
            SessionId = Guid.NewGuid(),
            StartAt = DateTime.UtcNow,
            EndAt = DateTime.UtcNow.AddMinutes(1),
            Application = "chrome.exe",
            Metrics = null
        };

        await service.ProcessKeyboardEventAsync(dto, Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(1, await db.KeyboardSessions.CountAsync());
    }

    [Fact]
    public async Task GetSummaryAsync_ReportsComputedClassification_NotHardcoded()
    {
        var db = CreateDb(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var start = new DateTime(2026, 8, 7, 10, 0, 0, DateTimeKind.Utc);

        db.KeyboardSessions.Add(new KeyboardSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = orgId,
            Application = "notepad.exe",
            StartAt = start,
            EndAt = start.AddMinutes(1),
            TotalKeystrokes = 10,
            WordsCount = 1,
            ProductivityScore = 10, // below the 40-point "Neutro" threshold
            Classification = KeyboardClassification.Improdutivo
        });
        await db.SaveChangesAsync();

        var repo = new KeyboardRepository(db);
        var summary = await repo.GetSummaryAsync(userId, start.AddMinutes(-1), start.AddMinutes(5));

        Assert.Equal(KeyboardClassification.Improdutivo, summary.Classification);
    }

    [Fact]
    public async Task ProcessMouseEventAsync_PersistsClickAndScrollCounts()
    {
        var db = CreateDb(Guid.NewGuid().ToString());
        var service = new MouseService(new MouseRepository(db));
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        var dto = new MouseEventCreateDTO
        {
            SessionId = Guid.NewGuid(),
            StartAt = DateTime.UtcNow,
            EndAt = DateTime.UtcNow.AddMinutes(1),
            Application = "chrome.exe",
            Metrics = new MouseMetricsDTO
            {
                LeftClicks = 5,
                RightClicks = 1,
                MiddleClicks = 0,
                ScrollCount = 20
            }
        };

        await service.ProcessMouseEventAsync(dto, userId, orgId);

        var session = await db.MouseSessions.FirstAsync();
        Assert.Equal(5, session.LeftClicks);
        Assert.Equal(1, session.RightClicks);
        Assert.Equal(20, session.ScrollCount);
        Assert.Equal(orgId, session.OrganizationId);
    }

    [Fact]
    public async Task ProcessMouseEventAsync_NullMetrics_DoesNotThrow()
    {
        var db = CreateDb(Guid.NewGuid().ToString());
        var service = new MouseService(new MouseRepository(db));

        var dto = new MouseEventCreateDTO
        {
            SessionId = Guid.NewGuid(),
            StartAt = DateTime.UtcNow,
            EndAt = DateTime.UtcNow.AddMinutes(1),
            Metrics = null
        };

        await service.ProcessMouseEventAsync(dto, Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(1, await db.MouseSessions.CountAsync());
    }
}
