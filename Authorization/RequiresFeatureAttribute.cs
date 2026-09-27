using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WiseMonitor.Api.Models.Billing;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Authorization
{
    /// <summary>
    /// Bloqueia o endpoint quando o plano da organização não inclui a feature.
    /// Responde 403 com <c>code = FEATURE_NOT_IN_PLAN</c> para o frontend exibir o upsell.
    /// SuperAdmin de plataforma e requests sem organização (agent anônimo) passam direto.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class RequiresFeatureAttribute : TypeFilterAttribute
    {
        public RequiresFeatureAttribute(string feature) : base(typeof(RequiresFeatureFilter))
        {
            Arguments = new object[] { feature };
        }
    }

    public class RequiresFeatureFilter : IAsyncActionFilter
    {
        private readonly string _feature;
        private readonly ITenantContext _tenant;
        private readonly IEntitlementService _entitlements;

        public RequiresFeatureFilter(string feature, ITenantContext tenant, IEntitlementService entitlements)
        {
            _feature = feature;
            _tenant = tenant;
            _entitlements = entitlements;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (_tenant.IsSuperAdmin || _tenant.OrganizationId is not Guid orgId)
            {
                await next();
                return;
            }

            if (await _entitlements.HasFeatureAsync(orgId, _feature, context.HttpContext.RequestAborted))
            {
                await next();
                return;
            }

            context.Result = FeatureNotInPlan(_feature);
        }

        public static ObjectResult FeatureNotInPlan(string feature) =>
            new(new
            {
                code = "FEATURE_NOT_IN_PLAN",
                feature,
                requiredPlan = PlanCatalog.LowestPlanWith(feature),
                message = "Este recurso não está incluído no plano atual da sua organização.",
            })
            { StatusCode = StatusCodes.Status403Forbidden };
    }
}
