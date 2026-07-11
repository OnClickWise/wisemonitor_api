using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WiseMonitor.Api.DTOs
{
    public class KeyboardMetricsDTO
    {
        public int TotalKeystrokes { get; set; }
        public int Letters { get; set; }
        public int Words { get; set; }
        public int Numbers { get; set; }
        public int Symbols { get; set; }
    }
}