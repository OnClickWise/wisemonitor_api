using System;
using Microsoft.AspNetCore.Http;
using WiseMonitor.Api.Models.Enums;

namespace WiseMonitor.Api.Services
{
    public class TenantContext : ITenantContext
    {
        public Guid? OrganizationId { get; }
        public bool IsSuperAdmin { get; }
        public bool IsActive { get; }

        public TenantContext(IHttpContextAccessor httpContextAccessor)
        {
            var user = httpContextAccessor.HttpContext?.User;
            IsActive = user?.Identity?.IsAuthenticated == true;

            if (!IsActive)
                return;

            IsSuperAdmin = string.Equals(user!.FindFirst("isSuperAdmin")?.Value, "true", StringComparison.OrdinalIgnoreCase)
                        || user.IsInRole(UserRoles.SuperAdmin);

            var orgIdClaim = user.FindFirst("orgId")?.Value;
            OrganizationId = Guid.TryParse(orgIdClaim, out var orgId) ? orgId : null;
        }
    }

    /// <summary>Usado quando não há ITenantContext registrado (testes, design-time/migrations) — nunca filtra.</summary>
    public class NullTenantContext : ITenantContext
    {
        public Guid? OrganizationId => null;
        public bool IsSuperAdmin => false;
        public bool IsActive => false;
    }
}
