using System;
using System.Threading;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Tests
{
    public class FakeDeviceSimulator
    {
        private readonly LiveMonitoringService _liveService;
        private readonly string _deviceId;
        private readonly string _orgId;
        private readonly string _username;
        private readonly string _department;
        private Timer? _timer;
        private readonly Random _rand = new();

        public FakeDeviceSimulator(
            LiveMonitoringService liveService, 
            string deviceId, 
            string orgId, 
            string username, 
            string department)
        {
            _liveService = liveService ?? throw new ArgumentNullException(nameof(liveService));
            _deviceId = deviceId;
            _orgId = orgId;
            _username = username;
            _department = department;
        }

        public void Start()
        {
            Console.WriteLine($"[FakeDevice] Iniciando simulação para {_username} ({_department})...");
            _timer = new Timer(SendUpdate, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        private void SendUpdate(object? state)
        {
            var fakeBytes = new byte[256]; // gera screenshot fake
            _rand.NextBytes(fakeBytes);

            Console.WriteLine($"[FakeDevice] Update enviado -> User: {_username}, Dept: {_department}, Device: {_deviceId}");
        }

        public void Stop()
        {
            Console.WriteLine($"[FakeDevice] Parando simulação do device {_deviceId}");
            _timer?.Dispose();
        }
    }
}
