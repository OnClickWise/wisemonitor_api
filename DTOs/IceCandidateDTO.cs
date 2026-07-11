using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WiseMonitor.Api.DTOs
{
    public class IceCandidateDTO1
    {
        public string Candidate { get; set; } = string.Empty;
        public string SdpMid { get; set; } = string.Empty;
        public int SdpMLineIndex { get; set; }
    }
}
                