using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WiseMonitor.Api.Models
{
    public class PasswordResetToken
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; } 
        public string Token { get; set; } = string.Empty;
        public DateTime Expiration { get; set; }
        public bool Used { get; set; } = false;
    }
}
