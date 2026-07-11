using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WiseMonitor.Api.DTOs.SuperAdmin
{
    public class PlatformOverviewDTO
    {
        // Tenants
        public int TotalTenants { get; set; }
        public int ActiveTenants { get; set; }
        public int SuspendedTenants { get; set; }
        public int TrialTenants { get; set; }
        public int NewTenantsLast30d { get; set; }
        public int ChurnLast30d { get; set; }

        // Usuários
        public int TotalUsers { get; set; }
        public int ActiveUsersLast7d { get; set; }
        public int ActiveUsersLast30d { get; set; }

        // Dispositivos e monitoramento
        public int TotalDevices { get; set; }
        public int OnlineDevicesNow { get; set; }
        public int ScreenshotsTakenLast24h { get; set; }
        public double StorageUsedGbTotal { get; set; }

        // Saúde do sistema
        public double ApiAvgResponseTimeMs { get; set; }
        public double ApiErrorRateLast1h { get; set; }
        public int QueuePendingJobs { get; set; }
    }

    public class PlanDistributionItemDTO
    {
        public string Plan { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percentage { get; set; }
    }

    public class TimeseriesQueryDTO
    {
        // NewTenants | ActiveUsers | Screenshots | StorageGb | ApiCalls | Errors
        [Required]
        public string Metric { get; set; } = "NewTenants";

        // 7d | 30d | 90d | 1y
        [Required]
        public string Period { get; set; } = "30d";

        // hour | day | week | month
        [Required]
        public string Granularity { get; set; } = "day";
    }

    public class TimeseriesPointDTO
    {
        public DateTime Date { get; set; }
        public double Value { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public class SystemHealthDTO
    {
        public string Status { get; set; } = "Healthy";
        public bool DatabaseConnected { get; set; }
        public int TotalMigrations { get; set; }
        public DateTime ServerTimeUtc { get; set; } = DateTime.UtcNow;
        public double UptimeHours { get; set; }
    }
}
