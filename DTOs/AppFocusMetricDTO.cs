using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.DTOs
{
    public class AppFocusMetricDTO
    {
        public string ApplicationName { get; set; } = string.Empty;
        public long TotalSeconds { get; set; }
        public ActivityCategory Category { get; set; }

    }
}