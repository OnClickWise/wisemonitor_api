using Microsoft.Extensions.Logging.Abstractions;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Services;
using Xunit;

namespace WiseMonitor.Api.Tests.Services;

public class LiveMonitoringServiceTests
{
    private static LiveMonitoringService CreateService() => new(NullLogger<LiveMonitoringService>.Instance);

    [Fact]
    public void AddWatcher_FiresWatchStateChangedOnlyOnFirstWatcher()
    {
        var service = CreateService();
        var events = new List<(string DeviceId, bool HasWatchers)>();
        service.WatchStateChanged += (deviceId, hasWatchers) => events.Add((deviceId, hasWatchers));

        service.AddWatcher("device-1", "session-a");
        service.AddWatcher("device-1", "session-b"); // segundo watcher do mesmo device

        Assert.Single(events);
        Assert.Equal(("device-1", true), events[0]);
        Assert.True(service.IsWatched("device-1"));
    }

    [Fact]
    public void RemoveWatcher_FiresWatchStateChangedOnlyWhenLastWatcherLeaves()
    {
        var service = CreateService();
        service.AddWatcher("device-1", "session-a");
        service.AddWatcher("device-1", "session-b");

        var events = new List<(string DeviceId, bool HasWatchers)>();
        service.WatchStateChanged += (deviceId, hasWatchers) => events.Add((deviceId, hasWatchers));

        service.RemoveWatcher("device-1", "session-a");
        Assert.Empty(events); // ainda tem session-b assistindo
        Assert.True(service.IsWatched("device-1"));

        service.RemoveWatcher("device-1", "session-b");
        Assert.Single(events);
        Assert.Equal(("device-1", false), events[0]);
        Assert.False(service.IsWatched("device-1"));
    }

    [Fact]
    public void RemoveWatcherFromAllDevices_FiresWatchStateChangedForEachDeviceThatLosesItsLastWatcher()
    {
        var service = CreateService();
        service.AddWatcher("device-1", "session-a");
        service.AddWatcher("device-2", "session-a");
        service.AddWatcher("device-2", "session-b");

        var events = new List<string>();
        service.WatchStateChanged += (deviceId, hasWatchers) =>
        {
            if (!hasWatchers) events.Add(deviceId);
        };

        service.RemoveWatcherFromAllDevices("session-a");

        Assert.Contains("device-1", events); // perdeu o único watcher
        Assert.DoesNotContain("device-2", events); // session-b ainda assiste
        Assert.True(service.IsWatched("device-2"));
    }

    [Fact]
    public void MarkDeviceOffline_ReflectsImmediatelyInGetLiveDevice()
    {
        var service = CreateService();
        service.RegisterOrUpdateDevice(new LiveDeviceUpdateDTO
        {
            DeviceId = "device-1",
            OrgId = "org-1",
            Status = "online"
        });

        service.MarkDeviceOffline("device-1");

        var device = service.GetLiveDevice("device-1");
        Assert.NotNull(device);
        Assert.Equal("offline", device!.Status);
    }

    [Fact]
    public void BroadcastExpiredDevices_MarksStaleDevicesOfflineAndLeavesFreshOnesAlone()
    {
        var service = CreateService();

        service.RegisterOrUpdateDevice(new LiveDeviceUpdateDTO
        {
            DeviceId = "stale-device",
            OrgId = "org-1",
            Status = "online",
            Timestamp = DateTime.UtcNow - LiveMonitoringService.PresenceTtl - TimeSpan.FromSeconds(30)
        });
        service.RegisterOrUpdateDevice(new LiveDeviceUpdateDTO
        {
            DeviceId = "fresh-device",
            OrgId = "org-1",
            Status = "online",
            Timestamp = DateTime.UtcNow
        });

        var expired = service.BroadcastExpiredDevices();

        Assert.Contains("stale-device", expired);
        Assert.DoesNotContain("fresh-device", expired);
        Assert.Equal("offline", service.GetLiveDevice("stale-device")!.Status);
        Assert.Equal("online", service.GetLiveDevice("fresh-device")!.Status);
    }

    [Fact]
    public void GetLiveDevice_DerivesOfflineFromStaleTimestampWithoutSweeper()
    {
        var service = CreateService();
        service.RegisterOrUpdateDevice(new LiveDeviceUpdateDTO
        {
            DeviceId = "device-1",
            OrgId = "org-1",
            Status = "online",
            Timestamp = DateTime.UtcNow - LiveMonitoringService.PresenceTtl - TimeSpan.FromSeconds(30)
        });

        // Mesmo sem o sweeper ter rodado, uma leitura direta já deriva "offline"
        // a partir da idade do timestamp.
        Assert.Equal("offline", service.GetLiveDevice("device-1")!.Status);

        // Um sinal novo "desempaca" o device — não fica preso em offline para sempre.
        service.RegisterOrUpdateDevice(new LiveDeviceUpdateDTO
        {
            DeviceId = "device-1",
            OrgId = "org-1",
            Status = "online",
            Timestamp = DateTime.UtcNow
        });
        Assert.Equal("online", service.GetLiveDevice("device-1")!.Status);
    }
}
