using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.Models.Billing;

namespace WiseMonitor.Api.Services
{
    /// <summary>
    /// Resolve o que uma organização pode usar: features do plano + add-ons ativos,
    /// retenção e licenças. Scoped — o snapshot é memorizado durante o request.
    /// </summary>
    public class EntitlementService : IEntitlementService
    {
        private readonly AppDbContext _context;
        private readonly Dictionary<Guid, EntitlementSnapshot?> _cache = new();

        public EntitlementService(AppDbContext context) => _context = context;

        public async Task<EntitlementSnapshot?> GetAsync(Guid organizationId, CancellationToken ct = default)
        {
            if (_cache.TryGetValue(organizationId, out var cached))
                return cached;

            var org = await _context.Organizations
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(o => o.Id == organizationId)
                .Select(o => new
                {
                    o.Id, o.Plan, o.Status, o.BillingCycle, o.TrialEndsAt, o.NextBillingAt, o.MaxUsers,
                })
                .FirstOrDefaultAsync(ct);

            if (org == null)
            {
                _cache[organizationId] = null;
                return null;
            }

            var now = DateTime.UtcNow;
            var addOns = await _context.OrganizationAddOns
                .AsNoTracking()
                .Where(a => a.OrganizationId == organizationId && a.IsActive
                         && a.StartedAt <= now && (a.EndsAt == null || a.EndsAt > now))
                .Select(a => new { a.Code, a.Quantity })
                .ToListAsync(ct);

            var seatsUsed = await _context.Users
                .IgnoreQueryFilters()
                .CountAsync(u => u.OrganizationId == organizationId && u.IsActive, ct);

            var isTrial = string.Equals(org.Status, "Trial", StringComparison.OrdinalIgnoreCase);
            var trialExpired = isTrial && org.TrialEndsAt.HasValue && org.TrialEndsAt.Value <= now;

            // Trial expirado mantém os dados visíveis mas cai para o conjunto Starter
            // até o cliente escolher um plano.
            var plan = trialExpired ? PlanCatalog.Get(PlanCodes.Starter) : PlanCatalog.Get(org.Plan);

            var features = new HashSet<string>(plan.Features);
            foreach (var addOn in addOns)
            {
                if (PlanCatalog.AddOnFeatures.TryGetValue(addOn.Code, out var granted))
                    features.UnionWith(granted);
            }

            var retention = plan.RetentionDays + addOns
                .Where(a => a.Code == AddOnCodes.ExtendedStorage)
                .Sum(a => Math.Max(0, a.Quantity));

            int? daysRemaining = isTrial && org.TrialEndsAt.HasValue
                ? Math.Max(0, (int)Math.Ceiling((org.TrialEndsAt.Value - now).TotalDays))
                : null;

            var snapshot = new EntitlementSnapshot
            {
                OrganizationId = org.Id,
                Plan = PlanCatalog.Normalize(org.Plan),
                Status = org.Status,
                BillingCycle = org.BillingCycle,
                IsTrial = isTrial,
                TrialExpired = trialExpired,
                TrialEndsAt = org.TrialEndsAt,
                TrialDaysRemaining = daysRemaining,
                NextBillingAt = org.NextBillingAt,
                SeatsPurchased = isTrial && !trialExpired ? null : org.MaxUsers,
                SeatsUsed = seatsUsed,
                RetentionDays = retention,
                Features = features,
                AddOns = addOns.Select(a => a.Code).Distinct().ToList(),
            };

            _cache[organizationId] = snapshot;
            return snapshot;
        }

        public async Task<bool> HasFeatureAsync(Guid organizationId, string feature, CancellationToken ct = default)
        {
            var snapshot = await GetAsync(organizationId, ct);
            return snapshot?.Has(feature) == true;
        }
    }
}
