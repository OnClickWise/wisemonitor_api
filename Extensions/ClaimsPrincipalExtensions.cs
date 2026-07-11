using System;
using System.Security.Claims;

namespace WiseMonitor.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst("userId") ?? user.FindFirst(ClaimTypes.NameIdentifier);
        return claim == null ? Guid.Empty : Guid.Parse(claim.Value);
    }

    public static Guid GetOrganizationId(this ClaimsPrincipal user)
    {
        var claim =
            user.FindFirst("organizationId") ??
            user.FindFirst("OrganizationId") ??
            user.FindFirst("orgId");

        return claim == null ? Guid.Empty : Guid.Parse(claim.Value);
    }
}
