using System.Threading.Tasks;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Services;

public interface IActivityClassificationService
{
    Task<ActivityCategory> ClassifyAsync(string applicationName);
}
