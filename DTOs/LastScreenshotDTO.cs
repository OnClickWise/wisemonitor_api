using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WiseMonitor.Api.DTOs
{
      
    public class LastScreenshotDTO
    {
        public Guid UserId { get; set; }
        public string? ContentType { get; set; }
        public string? Base64Image { get; set; }
        public DateTime CapturedAt { get; set; }
    }

}