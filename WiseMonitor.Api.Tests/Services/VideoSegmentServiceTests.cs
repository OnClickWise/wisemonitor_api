using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WiseMonitor.Api.Data;
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

file class FakeLiveMonitoringService : ILiveMonitoringService
{
    public int NotifyCallCount { get; private set; }

    public void UpdateDevice(string deviceId, string orgId, string username, string department, string thumbnailUrl, string fullScreenUrl, string type = "screenshot", string payload = "") { }
    public void RegisterOrUpdateDevice(WiseMonitor.Api.DTOs.LiveDeviceUpdateDTO dto) { }
    public void RegisterAdmin(string orgId, string sessionId, System.Net.WebSockets.WebSocket adminSocket) { }
    public void UnregisterAdmin(string orgId, string sessionId) { }
    public void AddWatcher(string deviceId, string sessionId) { }
    public void RemoveWatcher(string deviceId, string sessionId) { }
    public void RemoveWatcherFromAllDevices(string sessionId) { }
    public IReadOnlyList<WiseMonitor.Api.DTOs.MonitoringMessageDto> GetCachedMessages(string orgId) => Array.Empty<WiseMonitor.Api.DTOs.MonitoringMessageDto>();
    public WiseMonitor.Api.DTOs.MonitoringMessageDto? GetLiveDevice(string deviceId) => null;
    public IReadOnlyCollection<WiseMonitor.Api.DTOs.MonitoringMessageDto> GetAllLiveDevices() => Array.Empty<WiseMonitor.Api.DTOs.MonitoringMessageDto>();
    public Task BroadcastFrameAsync(string deviceId, WiseMonitor.Api.DTOs.MonitoringMessageDto frame) => Task.CompletedTask;

    public Task NotifyNewSegmentAsync(string deviceId, string orgId, Guid segmentId, DateTime startedAt, DateTime endedAt)
    {
        NotifyCallCount++;
        return Task.CompletedTask;
    }
}

public class VideoSegmentServiceTests
{
    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options,
            new FakeTenantContext { IsActive = false });

    private static IConfiguration CreateConfig(double retentionHours = 4) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["VideoSegmentRetentionHours"] = retentionHours.ToString()
            })
            .Build();

    // ─── Retenção (VideoSegmentRepository.UpsertAsync) ────────────────

    [Fact]
    public async Task UpsertAsync_PrunesSegmentsOlderThanRetentionWindow()
    {
        var db = CreateDb(Guid.NewGuid().ToString());
        var repo = new VideoSegmentRepository(db);
        var orgId = Guid.NewGuid();
        var deviceId = "device-1";

        var oldSegment = new VideoSegment
        {
            OrganizationId = orgId,
            MonitoredUserId = Guid.NewGuid(),
            DeviceId = deviceId,
            StartedAt = DateTime.UtcNow.AddHours(-10),
            EndedAt = DateTime.UtcNow.AddHours(-10).AddSeconds(10),
            VideoData = new byte[] { 1, 2, 3 }
        };
        await repo.UpsertAsync(oldSegment, TimeSpan.FromHours(4));

        var newSegment = new VideoSegment
        {
            OrganizationId = orgId,
            MonitoredUserId = Guid.NewGuid(),
            DeviceId = deviceId,
            StartedAt = DateTime.UtcNow,
            EndedAt = DateTime.UtcNow.AddSeconds(10),
            VideoData = new byte[] { 4, 5, 6 }
        };
        await repo.UpsertAsync(newSegment, TimeSpan.FromHours(4));

        var remaining = await db.VideoSegments.ToListAsync();

        Assert.Single(remaining);
        Assert.Equal(newSegment.Id, remaining[0].Id);
    }

    [Fact]
    public async Task UpsertAsync_KeepsSegmentsWithinRetentionWindow()
    {
        var db = CreateDb(Guid.NewGuid().ToString());
        var repo = new VideoSegmentRepository(db);
        var orgId = Guid.NewGuid();
        var deviceId = "device-1";

        var recentSegment = new VideoSegment
        {
            OrganizationId = orgId,
            MonitoredUserId = Guid.NewGuid(),
            DeviceId = deviceId,
            StartedAt = DateTime.UtcNow.AddMinutes(-30),
            EndedAt = DateTime.UtcNow.AddMinutes(-30).AddSeconds(10),
            VideoData = new byte[] { 1 }
        };
        await repo.UpsertAsync(recentSegment, TimeSpan.FromHours(4));

        var remaining = await db.VideoSegments.ToListAsync();
        Assert.Single(remaining);
    }

    // ─── Correlação (VideoSegmentService.GetHistoryWithContextAsync) ──

    [Fact]
    public async Task GetHistoryWithContextAsync_ReturnsOverlappingAppFocusAndKeyboardContext()
    {
        var db = CreateDb(Guid.NewGuid().ToString());
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deviceId = "device-1";
        var segmentStart = new DateTime(2026, 7, 12, 10, 0, 0, DateTimeKind.Utc);
        var segmentEnd = segmentStart.AddSeconds(10);

        db.VideoSegments.Add(new VideoSegment
        {
            OrganizationId = orgId,
            MonitoredUserId = userId,
            DeviceId = deviceId,
            StartedAt = segmentStart,
            EndedAt = segmentEnd,
            VideoData = new byte[] { 1 }
        });

        // Overlaps the segment window
        db.AppFocusEvents.Add(new AppFocusEvent
        {
            OrganizationId = orgId,
            UserId = userId,
            DeviceId = Guid.NewGuid(),
            ApplicationName = "chrome.exe",
            WindowTitle = "Overlapping window",
            StartTime = segmentStart.AddSeconds(-5),
            EndTime = segmentStart.AddSeconds(5)
        });

        // Does NOT overlap (starts after segment ends)
        db.AppFocusEvents.Add(new AppFocusEvent
        {
            OrganizationId = orgId,
            UserId = userId,
            DeviceId = Guid.NewGuid(),
            ApplicationName = "notepad.exe",
            WindowTitle = "Non-overlapping window",
            StartTime = segmentEnd.AddMinutes(1),
            EndTime = segmentEnd.AddMinutes(2)
        });

        var keyboardSession = new KeyboardSession
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            UserId = userId,
            Application = "chrome.exe",
            StartAt = segmentStart.AddSeconds(-2),
            EndAt = segmentStart.AddSeconds(8),
            TotalKeystrokes = 42,
            WordsCount = 2
        };
        keyboardSession.Words.Add(new KeyboardWord { Id = Guid.NewGuid(), Word = "hello", Count = 3 });
        keyboardSession.Words.Add(new KeyboardWord { Id = Guid.NewGuid(), Word = "world", Count = 1 });
        db.KeyboardSessions.Add(keyboardSession);

        await db.SaveChangesAsync();

        var service = new VideoSegmentService(
            new VideoSegmentRepository(db), db, new FakeLiveMonitoringService(), CreateConfig());

        var history = (await service.GetHistoryWithContextAsync(
            deviceId, segmentStart.AddMinutes(-1), segmentEnd.AddMinutes(1), "http://localhost:8080")).ToList();

        Assert.Single(history);
        var item = history[0];

        Assert.Single(item.Context.AppFocusEvents);
        Assert.Equal("chrome.exe", item.Context.AppFocusEvents[0].ApplicationName);

        Assert.Single(item.Context.KeyboardSessions);
        Assert.Equal(42, item.Context.KeyboardSessions[0].TotalKeystrokes);
        Assert.Equal(new[] { "hello", "world" }, item.Context.KeyboardSessions[0].TopWords);
    }

    [Fact]
    public async Task SaveSegmentAsync_NotifiesLiveMonitoringService()
    {
        var db = CreateDb(Guid.NewGuid().ToString());
        var fakeLive = new FakeLiveMonitoringService();
        var service = new VideoSegmentService(new VideoSegmentRepository(db), db, fakeLive, CreateConfig());

        using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var formFile = new Microsoft.AspNetCore.Http.FormFile(stream, 0, stream.Length, "Segment", "segment.mp4")
        {
            Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(),
            ContentType = "video/mp4"
        };

        await service.SaveSegmentAsync(new WiseMonitor.Api.DTOs.VideoSegmentUploadDTO
        {
            Segment = formFile,
            DeviceId = "device-1",
            OrganizationId = Guid.NewGuid(),
            MonitoredUserId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow,
            EndedAt = DateTime.UtcNow.AddSeconds(10)
        });

        Assert.Equal(1, fakeLive.NotifyCallCount);
        Assert.Equal(1, await db.VideoSegments.CountAsync());
    }
}
