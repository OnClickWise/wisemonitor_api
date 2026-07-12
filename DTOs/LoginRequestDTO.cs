using System;

namespace WiseMonitor.Api.DTOs
{
    public class LoginRequestDTO
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
        public Guid OrganizationId { get; set; }
    }
}