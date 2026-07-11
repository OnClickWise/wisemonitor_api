using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WiseMonitor.Api.Models
{
    public class LiveSession
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid SessionId { get; set; } 
        public DateTime CreatedAt { get; set; }
    }
}