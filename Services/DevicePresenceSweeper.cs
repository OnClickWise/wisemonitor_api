using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WiseMonitor.Api.Repositories;

namespace WiseMonitor.Api.Services
{
    /// <summary>
    /// Rede de segurança do status online.
    ///
    /// O agent avisa quando está desligando, e nesse caminho o offline é imediato.
    /// Só que existe todo um conjunto de desligamentos em que nenhum aviso chega:
    /// queda de energia, bateria acabando, cabo de rede removido, processo morto,
    /// máquina hibernando. Sem este varredor, esses devices ficariam marcados como
    /// online até alguém reiniciar o backend.
    ///
    /// Roda tanto no banco (Devices.IsOnline) quanto no cache em memória do live
    /// monitoring, que é o que o painel realmente consome via WebSocket.
    /// </summary>
    public class DevicePresenceSweeper : BackgroundService
    {
        private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(15);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILiveMonitoringService _liveService;
        private readonly ILogger<DevicePresenceSweeper> _logger;

        public DevicePresenceSweeper(
            IServiceScopeFactory scopeFactory,
            ILiveMonitoringService liveService,
            ILogger<DevicePresenceSweeper> logger)
        {
            _scopeFactory = scopeFactory;
            _liveService = liveService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "[PresenceSweeper] Iniciado — varredura a cada {Interval}s, TTL de {Ttl}s.",
                SweepInterval.TotalSeconds, LiveMonitoringService.PresenceTtl.TotalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SweepAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Falha de banco aqui não pode derrubar o hosted service: se ele
                    // morrer, ninguém mais volta a marcar device offline.
                    _logger.LogError(ex, "[PresenceSweeper] Falha na varredura.");
                }

                try
                {
                    await Task.Delay(SweepInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task SweepAsync(CancellationToken ct)
        {
            var threshold = DateTime.UtcNow - LiveMonitoringService.PresenceTtl;

            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();

            var affected = await repository.MarkStaleOfflineAsync(threshold, ct);

            var expired = _liveService.BroadcastExpiredDevices();

            if (affected > 0 || expired.Count > 0)
            {
                _logger.LogInformation(
                    "[PresenceSweeper] {DbCount} device(s) marcados offline no banco, {CacheCount} no cache ao vivo.",
                    affected, expired.Count);
            }
        }
    }
}
