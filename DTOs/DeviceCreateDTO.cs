using System;

namespace WiseMonitor.Api.DTOs
{


    public class DeviceCreateDTO
    {
        public string Hostname { get; set; } = string.Empty;
        public string AgentEmail { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
         public string IpAddress { get; set; } = string.Empty;
    }

}
