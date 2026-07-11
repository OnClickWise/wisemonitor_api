using System;
using System.ComponentModel.DataAnnotations;

namespace WiseMonitor.Api.Models
{
    public class Delegation
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid OrganizationId { get; set; }

        // Quem delega
        public Guid DelegatorId { get; set; }
        public User Delegator { get; set; } = null!;

        // Quem recebe
        public Guid DelegateId { get; set; }
        public User Delegate { get; set; } = null!;

        // JSON com escopo: { teamIds: [], departmentIds: [], permissions: [] }
        [Required]
        public string Scope { get; set; } = "{}";

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public bool IsActive { get; set; } = true;

        [MaxLength(500)]
        public string? Reason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? RevokedAt { get; set; }
        public Guid? RevokedBy { get; set; }
    }
}
