using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WiseMonitor.Api.Services
{
    public sealed class EntitlementSnapshot
    {
        public Guid OrganizationId { get; init; }
        public string Plan { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string BillingCycle { get; init; } = string.Empty;

        public bool IsTrial { get; init; }
        public bool TrialExpired { get; init; }
        public DateTime? TrialEndsAt { get; init; }
        public int? TrialDaysRemaining { get; init; }

        public DateTime? NextBillingAt { get; init; }

        public int? SeatsPurchased { get; init; }
        public int SeatsUsed { get; init; }

        public int RetentionDays { get; init; }
        public IReadOnlySet<string> Features { get; init; } = new HashSet<string>();
        public IReadOnlyCollection<string> AddOns { get; init; } = Array.Empty<string>();

        public bool Has(string feature) => Features.Contains(feature);
        public bool HasSeatAvailable => SeatsPurchased is null || SeatsUsed < SeatsPurchased;
    }

    public interface IEntitlementService
    {
        Task<EntitlementSnapshot?> GetAsync(Guid organizationId, CancellationToken ct = default);
        Task<bool> HasFeatureAsync(Guid organizationId, string feature, CancellationToken ct = default);
    }
}
