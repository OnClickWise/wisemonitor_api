using System;
using System.Collections.Generic;
using System.Linq;

namespace WiseMonitor.Api.Models.Billing
{
    public static class PlanCodes
    {
        public const string Starter      = "Starter";
        public const string Professional = "Professional";
        public const string Business     = "Business";
        public const string Enterprise   = "Enterprise";
    }

    public static class BillingCycles
    {
        public const string Monthly = "Monthly";
        public const string Annual  = "Annual";
    }

    public static class AddOnCodes
    {
        public const string AiInsights      = "AI_INSIGHTS";
        public const string ExtendedStorage = "EXTENDED_STORAGE";
        public const string Dlp             = "DLP";
        public const string PremiumSupport  = "PREMIUM_SUPPORT";
        public const string OnPremise       = "ON_PREMISE";
    }

    public sealed record PlanDefinition(
        string Code,
        int Rank,
        decimal MonthlyPricePerUserUsd,
        decimal AnnualPricePerUserUsd,
        decimal MinimumMonthlyUsd,
        int RetentionDays,
        bool SelfServiceCheckout,
        IReadOnlySet<string> Features);

    public sealed record VolumeDiscountTier(int MinSeats, int? MaxSeats, decimal DiscountPercent);

    public sealed record PriceQuote(
        string Plan,
        string BillingCycle,
        int Seats,
        decimal UnitPriceUsd,
        decimal ListPriceUsd,
        decimal DiscountPercent,
        decimal DiscountUsd,
        decimal TotalUsd,
        bool MinimumApplied,
        bool RequiresSales);

    /// <summary>
    /// Fonte única da matriz comercial. O frontend espelha estes valores em src/lib/plans.ts.
    /// </summary>
    public static class PlanCatalog
    {
        public const int TrialDays = 14;
        public const string TrialPlan = PlanCodes.Professional;

        /// <summary>Acima deste número de licenças o preço é negociado com Vendas.</summary>
        public const int NegotiationSeatThreshold = 1000;

        private static readonly string[] StarterFeatures =
        {
            Features.Dashboard, Features.TimeTracking, Features.ActivityTracking, Features.AppTracking,
            Features.Teams, Features.BasicProductivity, Features.BasicReports,
        };

        private static readonly string[] ProfessionalFeatures = StarterFeatures.Concat(new[]
        {
            Features.Screenshots, Features.ScreenshotInterval, Features.LiveStream, Features.UrlTracking,
            Features.Projects, Features.Tasks, Features.ProductivityAnalytics, Features.FullReports,
            Features.ReportsExport,
        }).ToArray();

        private static readonly string[] BusinessFeatures = ProfessionalFeatures.Concat(new[]
        {
            Features.Departments, Features.Alerts, Features.MonitoringPolicies, Features.ExecutiveAnalytics,
            Features.AdvancedReports, Features.ScheduledReports, Features.ApiAccess, Features.Webhooks,
        }).ToArray();

        private static readonly string[] EnterpriseFeatures = BusinessFeatures.Concat(new[]
        {
            Features.Sso, Features.AdvancedAudit, Features.CustomRetention, Features.CustomReports,
            Features.EnterpriseSecurity, Features.Sla, Features.DedicatedSupport,
        }).ToArray();

        public static readonly IReadOnlyList<PlanDefinition> Plans = new[]
        {
            new PlanDefinition(PlanCodes.Starter,      1,  3.90m, 39m, 10m,  30, true,  new HashSet<string>(StarterFeatures)),
            new PlanDefinition(PlanCodes.Professional, 2,  5.90m, 59m,  0m,  90, true,  new HashSet<string>(ProfessionalFeatures)),
            new PlanDefinition(PlanCodes.Business,     3,  8.90m, 89m,  0m, 180, true,  new HashSet<string>(BusinessFeatures)),
            new PlanDefinition(PlanCodes.Enterprise,   4, 12.90m,  0m,  0m, 365, false, new HashSet<string>(EnterpriseFeatures)),
        };

        public static readonly IReadOnlyList<VolumeDiscountTier> VolumeDiscounts = new[]
        {
            new VolumeDiscountTier(1,   10,   0m),
            new VolumeDiscountTier(11,  50,   5m),
            new VolumeDiscountTier(51,  100,  10m),
            new VolumeDiscountTier(101, 250,  15m),
            new VolumeDiscountTier(251, 500,  20m),
            new VolumeDiscountTier(501, 1000, 25m),
        };

        /// <summary>Features que cada add-on concede (EXTENDED_STORAGE altera retenção, não features).</summary>
        public static readonly IReadOnlyDictionary<string, string[]> AddOnFeatures = new Dictionary<string, string[]>
        {
            [AddOnCodes.AiInsights]     = new[] { Features.AiInsights },
            [AddOnCodes.Dlp]            = new[] { Features.Dlp },
            [AddOnCodes.PremiumSupport] = new[] { Features.PremiumSupport },
        };

        public static PlanDefinition Get(string? plan) =>
            Plans.First(p => p.Code == Normalize(plan));

        public static bool IsValid(string? plan) =>
            Plans.Any(p => string.Equals(p.Code, plan, StringComparison.OrdinalIgnoreCase));

        /// <summary>Converte nomes antigos (Free/Basic/Pro) e variações de caixa para o código oficial.</summary>
        public static string Normalize(string? plan)
        {
            if (string.IsNullOrWhiteSpace(plan))
                return PlanCodes.Starter;

            var match = Plans.FirstOrDefault(p => string.Equals(p.Code, plan.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return match.Code;

            return plan.Trim().ToLowerInvariant() switch
            {
                "pro"   => PlanCodes.Professional,
                "free"  => PlanCodes.Starter,
                "basic" => PlanCodes.Starter,
                _       => PlanCodes.Starter,
            };
        }

        /// <summary>Plano de menor rank que contém a feature — usado para sugerir upgrade.</summary>
        public static string? LowestPlanWith(string feature) =>
            Plans.OrderBy(p => p.Rank).FirstOrDefault(p => p.Features.Contains(feature))?.Code;

        public static decimal VolumeDiscountFor(int seats) =>
            VolumeDiscounts.LastOrDefault(t => seats >= t.MinSeats)?.DiscountPercent ?? 0m;

        public static PriceQuote Quote(string plan, int seats, string billingCycle)
        {
            var def = Get(plan);
            seats = Math.Max(1, seats);
            var annual = string.Equals(billingCycle, BillingCycles.Annual, StringComparison.OrdinalIgnoreCase);

            // Enterprise não tem preço anual tabelado: usa o mensal × 12 como referência.
            var unit = annual
                ? (def.AnnualPricePerUserUsd > 0 ? def.AnnualPricePerUserUsd : def.MonthlyPricePerUserUsd * 12)
                : def.MonthlyPricePerUserUsd;

            var list = unit * seats;
            var discountPct = VolumeDiscountFor(seats);
            var discount = Math.Round(list * discountPct / 100m, 2);
            var total = list - discount;

            var minimum = annual ? def.MinimumMonthlyUsd * 12 : def.MinimumMonthlyUsd;
            var minimumApplied = total < minimum;
            if (minimumApplied)
                total = minimum;

            return new PriceQuote(
                def.Code,
                annual ? BillingCycles.Annual : BillingCycles.Monthly,
                seats,
                unit,
                list,
                discountPct,
                discount,
                Math.Round(total, 2),
                minimumApplied,
                RequiresSales: !def.SelfServiceCheckout || seats > NegotiationSeatThreshold);
        }
    }
}
