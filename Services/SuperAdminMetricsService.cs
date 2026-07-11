using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs.SuperAdmin;

namespace WiseMonitor.Api.Services
{
    public class SuperAdminMetricsService : ISuperAdminMetricsService
    {
        private readonly AppDbContext _context;
        private static readonly DateTime _startupTime = DateTime.UtcNow;

        public SuperAdminMetricsService(AppDbContext context) => _context = context;

        public async Task<PlatformOverviewDTO> GetOverviewAsync(CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var ago30d = now.AddDays(-30);
            var ago7d = now.AddDays(-7);
            var ago24h = now.AddHours(-24);

            return new PlatformOverviewDTO
            {
                TotalTenants         = await _context.Organizations.CountAsync(ct),
                ActiveTenants        = await _context.Organizations.CountAsync(o => o.Status == "Active", ct),
                SuspendedTenants     = await _context.Organizations.CountAsync(o => o.Status == "Suspended", ct),
                TrialTenants         = await _context.Organizations.CountAsync(o => o.Status == "Trial", ct),
                NewTenantsLast30d    = await _context.Organizations.CountAsync(o => o.CreatedAt >= ago30d, ct),
                ChurnLast30d         = await _context.Organizations.CountAsync(o =>
                                           o.Status == "Suspended" && o.SuspendedAt >= ago30d, ct),
                TotalUsers           = await _context.Users.CountAsync(ct),
                ActiveUsersLast7d    = await _context.Users.CountAsync(u => u.IsActive && u.CreatedAt >= ago7d, ct),
                ActiveUsersLast30d   = await _context.Users.CountAsync(u => u.IsActive && u.CreatedAt >= ago30d, ct),
                TotalDevices         = await _context.Devices.CountAsync(ct),
                OnlineDevicesNow     = await _context.Devices.CountAsync(d => d.IsOnline, ct),
                ScreenshotsTakenLast24h = await _context.Screenshots.CountAsync(s => s.CapturedAt >= ago24h, ct),
                StorageUsedGbTotal   = 0,
                ApiAvgResponseTimeMs = 0,
                ApiErrorRateLast1h   = 0,
                QueuePendingJobs     = 0,
            };
        }

        public async Task<IEnumerable<TimeseriesPointDTO>> GetTimeseriesAsync(TimeseriesQueryDTO query, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;

            var from = query.Period switch
            {
                "7d"  => now.AddDays(-7),
                "90d" => now.AddDays(-90),
                "1y"  => now.AddDays(-365),
                _     => now.AddDays(-30),
            };

            if (query.Metric == "NewTenants")
            {
                var tenants = await _context.Organizations.AsNoTracking()
                    .Where(o => o.CreatedAt >= from)
                    .Select(o => o.CreatedAt.Date)
                    .ToListAsync(ct);

                return tenants
                    .GroupBy(d => d)
                    .Select(g => new TimeseriesPointDTO
                    {
                        Date  = g.Key,
                        Value = g.Count(),
                        Label = g.Key.ToString("dd/MM"),
                    })
                    .OrderBy(p => p.Date);
            }

            if (query.Metric == "ActiveUsers")
            {
                var users = await _context.Users.AsNoTracking()
                    .Where(u => u.IsActive && u.CreatedAt >= from)
                    .Select(u => u.CreatedAt.Date)
                    .ToListAsync(ct);

                return users
                    .GroupBy(d => d)
                    .Select(g => new TimeseriesPointDTO
                    {
                        Date  = g.Key,
                        Value = g.Count(),
                        Label = g.Key.ToString("dd/MM"),
                    })
                    .OrderBy(p => p.Date);
            }

            if (query.Metric == "Screenshots")
            {
                var shots = await _context.Screenshots.AsNoTracking()
                    .Where(s => s.CapturedAt >= from)
                    .Select(s => s.CapturedAt.Date)
                    .ToListAsync(ct);

                return shots
                    .GroupBy(d => d)
                    .Select(g => new TimeseriesPointDTO
                    {
                        Date  = g.Key,
                        Value = g.Count(),
                        Label = g.Key.ToString("dd/MM"),
                    })
                    .OrderBy(p => p.Date);
            }

            return [];
        }

        public async Task<IEnumerable<PlanDistributionItemDTO>> GetPlanDistributionAsync(CancellationToken ct = default)
        {
            var total = await _context.Organizations.CountAsync(ct);

            if (total == 0) return [];

            var groups = await _context.Organizations.AsNoTracking()
                .GroupBy(o => o.Plan)
                .Select(g => new { Plan = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            return groups.Select(g => new PlanDistributionItemDTO
            {
                Plan       = g.Plan,
                Count      = g.Count,
                Percentage = Math.Round((double)g.Count / total * 100, 1),
            });
        }

        public async Task<SystemHealthDTO> GetSystemHealthAsync(CancellationToken ct = default)
        {
            bool dbOk = false;
            try { dbOk = await _context.Database.CanConnectAsync(ct); }
            catch { /* swallow */ }

            return new SystemHealthDTO
            {
                Status            = dbOk ? "Healthy" : "Degraded",
                DatabaseConnected = dbOk,
                TotalMigrations   = (await _context.Database.GetAppliedMigrationsAsync(ct)).Count(),
                ServerTimeUtc     = DateTime.UtcNow,
                UptimeHours       = Math.Round((DateTime.UtcNow - _startupTime).TotalHours, 2),
            };
        }
    }
}
