using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WiseMonitor.Api.DTOs
{
    public class LiveDeviceUpdateDTO
    {

        public string DeviceId { get; set; } = "";
        public string Username { get; set; } = "";
        public string Department { get; set; } = "";
        public string Status { get; set; } = "offline"; // online/offline
        public string ThumbnailUrl { get; set; } = "";
        public string FullScreenUrl { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string OrgId { get; set; } = "default"; 
    }
}