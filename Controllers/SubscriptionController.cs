using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WiseMonitor.Api.Extensions;
using WiseMonitor.Api.Models.Billing;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Controllers
{
    [ApiController]
    [Route("api/subscription")]
    public class SubscriptionController : ControllerBase
    {
        private readonly IEntitlementService _entitlements;

        public SubscriptionController(IEntitlementService entitlements) => _entitlements = entitlements;

        /// <summary>Plano, trial, licenças, retenção e features da organização do usuário logado.</summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetMine(CancellationToken ct)
        {
            var orgId = User.GetOrganizationId();
            if (orgId == Guid.Empty)
                return NotFound(new { message = "Organização não encontrada." });

            var snapshot = await _entitlements.GetAsync(orgId, ct);
            if (snapshot == null)
                return NotFound(new { message = "Organização não encontrada." });

            return Ok(new
            {
                snapshot.Plan,
                snapshot.Status,
                snapshot.BillingCycle,
                snapshot.IsTrial,
                snapshot.TrialExpired,
                snapshot.TrialEndsAt,
                snapshot.TrialDaysRemaining,
                snapshot.NextBillingAt,
                snapshot.SeatsPurchased,
                snapshot.SeatsUsed,
                snapshot.RetentionDays,
                Features = snapshot.Features.OrderBy(f => f),
                snapshot.AddOns,
            });
        }

        /// <summary>Catálogo público de planos e descontos por volume.</summary>
        [HttpGet("plans")]
        [AllowAnonymous]
        public IActionResult GetPlans() => Ok(new
        {
            trialDays = PlanCatalog.TrialDays,
            trialPlan = PlanCatalog.TrialPlan,
            plans = PlanCatalog.Plans.Select(p => new
            {
                p.Code,
                p.MonthlyPricePerUserUsd,
                p.AnnualPricePerUserUsd,
                p.MinimumMonthlyUsd,
                p.RetentionDays,
                p.SelfServiceCheckout,
                Features = p.Features.OrderBy(f => f),
            }),
            volumeDiscounts = PlanCatalog.VolumeDiscounts,
            negotiationSeatThreshold = PlanCatalog.NegotiationSeatThreshold,
        });

        /// <summary>Simula o valor para N licenças em um plano/ciclo.</summary>
        [HttpGet("quote")]
        [AllowAnonymous]
        public IActionResult GetQuote([FromQuery] string plan, [FromQuery] int seats, [FromQuery] string cycle = BillingCycles.Monthly)
        {
            if (!PlanCatalog.IsValid(plan))
                return BadRequest(new { message = "Plano inválido." });
            if (seats < 1 || seats > 100_000)
                return BadRequest(new { message = "Quantidade de licenças inválida." });

            return Ok(PlanCatalog.Quote(plan, seats, cycle));
        }
    }
}
