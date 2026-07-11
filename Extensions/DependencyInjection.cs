using Microsoft.Extensions.DependencyInjection;
using WiseMonitor.Api.Repositories;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Extensions
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddKeyboardModule(
            this IServiceCollection services)
        {
            services.AddScoped<IKeyboardRepository, KeyboardRepository>();
            services.AddScoped<IKeyboardService, KeyboardService>();

            return services;
        }
    }
}