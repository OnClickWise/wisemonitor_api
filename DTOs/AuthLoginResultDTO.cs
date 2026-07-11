using System;

namespace WiseMonitor.Api.DTOs
{
    public class AuthLoginResultDTO
    {
        public string Token { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public Guid? OrganizationId { get; set; }
        public AuthLoginUserDTO User { get; set; } = new();
    }

    public class AuthLoginUserDTO
    {
        public Guid Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
    }
}
