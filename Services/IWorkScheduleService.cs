using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;

namespace WiseMonitor.Api.Services
{
    public class WorkScheduleUserDTO
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; }
    }

    public class WorkScheduleHistoryDTO
    {
        public Guid WorkScheduleId { get; set; }
        public string WorkScheduleName { get; set; } = string.Empty;
        public string TypeLabel { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; }
    }

    public class WorkScheduleCurrentDTO
    {
        public Guid WorkScheduleId { get; set; }
        public string WorkScheduleName { get; set; } = string.Empty;
        public string TypeLabel { get; set; } = string.Empty;
        public WorkScheduleRuleDTO? TodayRule { get; set; }
    }

    public interface IWorkScheduleService
    {
        Task<WorkScheduleResponseDTO> CreateAsync(Guid organizationId, WorkScheduleCreateDTO dto);
        Task<IEnumerable<WorkScheduleResponseDTO>> GetAllAsync(Guid organizationId);
        Task<WorkScheduleResponseDTO?> GetByIdAsync(Guid id);
        Task<WorkScheduleResponseDTO?> UpdateAsync(Guid id, WorkScheduleUpdateDTO dto);
        Task<bool> DeleteAsync(Guid id);
        Task<bool> AssignToUserAsync(AssignUserScheduleDTO dto);
        Task<WorkScheduleResponseDTO> CloneAsync(Guid id, string newName, Guid organizationId);
        Task<IEnumerable<WorkScheduleUserDTO>> GetUsersAsync(Guid scheduleId);
        Task<IEnumerable<WorkScheduleHistoryDTO>> GetUserHistoryAsync(Guid userId, Guid organizationId);
        Task<WorkScheduleCurrentDTO?> GetCurrentScheduleAsync(Guid userId, Guid organizationId);
    }
}
