using System;

namespace WiseMonitor.Api.DTOs
{
    public class RegisterOrganizationResultDTO
    {
        public Guid OrganizationId { get; set; }
        public Guid AdminUserId { get; set; }
        public string AdminEmail { get; set; } = string.Empty;
    }
}
