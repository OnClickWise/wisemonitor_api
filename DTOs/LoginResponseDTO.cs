using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WiseMonitor.Api.DTOs
{
    public class LoginResponseDTO
    {
        
        public string FullName { get; set; } = string.Empty;
        public string? Token { get; set; }
        public int ExpiresIn { get; set; } = 5000;
        public Guid UserId { get; set; }
        public string Role { get; set; } = default!;
        public Guid? OrganizationId { get; set; }
         public string? SessionId { get; set; }
        public DateTime ExpiresAt { get; set; }
    
    }
}