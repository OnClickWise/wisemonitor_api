using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.DTOs
{
    public class KeyboardSummaryDTO
    {
        public int TotalKeystrokes { get; set; }
        public int TotalWords { get; set; }
        public int ProductivityScore { get; set; }

        public KeyboardClassification Classification { get; set; }
    }
}