using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WiseMonitor.Api.Helpers
{
    public class AppFocusDurationHelper
    {
        public static long CalculateDuration(DateTime start, DateTime? end)
    {
        var endTime = end ?? DateTime.UtcNow;
        return Math.Max(0, (long)(endTime - start).TotalSeconds);
    }
    }
}