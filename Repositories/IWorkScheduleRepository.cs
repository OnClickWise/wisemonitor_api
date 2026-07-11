using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Repositories
{
    public interface IWorkScheduleRepository
    {
        Task CreateAsync(WorkSchedule schedule);
        Task UpdateAsync(WorkSchedule schedule);
        Task<bool> DeleteAsync(Guid id);
        Task<WorkSchedule?> GetByIdAsync(Guid id);
        Task<IEnumerable<WorkSchedule>> GetAllByOrganizationAsync(Guid organizationId);
        Task<bool> AssignScheduleToUserAsync(Guid userId, Guid scheduleId);
        Task<IEnumerable<WorkScheduleUserDTO>> GetScheduleUsersAsync(Guid scheduleId);
        Task<IEnumerable<WorkScheduleHistoryDTO>> GetUserScheduleHistoryAsync(Guid userId, Guid organizationId);
        Task<WorkScheduleCurrentDTO?> GetCurrentUserScheduleAsync(Guid userId, Guid organizationId);
    }
}
