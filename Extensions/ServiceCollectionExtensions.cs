using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WiseMonitor.Api.Configs;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAgentModule(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<AgentSettings>(config.GetSection("AgentSettings"));
            services.AddScoped<IAgentService, AgentService>();

            return services;
        }
    }
}