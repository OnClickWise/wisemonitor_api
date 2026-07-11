using System.Threading.Tasks;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Services;

public class ActivityClassificationService : IActivityClassificationService
{
    public Task<ActivityCategory> ClassifyAsync(string applicationName)
    {
        if (string.IsNullOrWhiteSpace(applicationName))
            return Task.FromResult(ActivityCategory.Neutral);

        var app = applicationName.ToLower();

        if (app.Contains("chrome") ||
            app.Contains("edge") ||
            app.Contains("firefox"))
            return Task.FromResult(ActivityCategory.Productive);

        if (app.Contains("spotify") ||
            app.Contains("youtube"))
            return Task.FromResult(ActivityCategory.Unproductive);

        return Task.FromResult(ActivityCategory.Neutral);
    }
}
