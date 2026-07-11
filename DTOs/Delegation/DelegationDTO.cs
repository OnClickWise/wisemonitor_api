using System;
using System.Collections.Generic;

namespace WiseMonitor.Api.DTOs.Delegation
{
    public class DelegationDTO
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid DelegatorId { get; set; }
        public string DelegatorName { get; set; } = string.Empty;
        public Guid DelegateId { get; set; }
        public string DelegateName { get; set; } = string.Empty;
        public DelegationScopeDTO Scope { get; set; } = new();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class DelegationScopeDTO
    {
        public List<Guid> TeamIds { get; set; } = new();
        public List<Guid> DepartmentIds { get; set; } = new();
        public List<string> Permissions { get; set; } = new();
    }

    public class CreateDelegationDTO
    {
        public Guid DelegateId { get; set; }
        public DelegationScopeDTO Scope { get; set; } = new();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Reason { get; set; }
    }
}
