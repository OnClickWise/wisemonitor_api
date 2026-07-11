using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Repositories;
using WiseMonitor.Api.Utils;

namespace WiseMonitor.Api.Services
{
    public class WorkScheduleService : IWorkScheduleService
    {
        private readonly IWorkScheduleRepository _repository;

        public WorkScheduleService(IWorkScheduleRepository repository)
        {
            _repository = repository;
        }

        public async Task<WorkScheduleResponseDTO> CreateAsync(Guid organizationId, WorkScheduleCreateDTO dto)
        {
            WorkScheduleValidator.ValidateCreate(dto);

            var schedule = new WorkSchedule
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = dto.Name,
                Description = dto.Description,
                ScheduleCode = dto.ScheduleCode,
                Type = (WorkScheduleType)dto.Type,
                MonitorIdleTime = dto.MonitorIdleTime,
                MonitorOutsideSchedule = dto.MonitorOutsideSchedule,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Rules = dto.Rules.Select(r => MapRuleFromDTO(r, organizationId)).ToList()
            };

            await _repository.CreateAsync(schedule);
            return WorkScheduleResponseDTO.FromEntity(schedule);
        }

        public async Task<IEnumerable<WorkScheduleResponseDTO>> GetAllAsync(Guid organizationId)
        {
            var schedules = await _repository.GetAllByOrganizationAsync(organizationId);
            return schedules.Select(WorkScheduleResponseDTO.FromEntity);
        }

        public async Task<WorkScheduleResponseDTO?> GetByIdAsync(Guid id)
        {
            var schedule = await _repository.GetByIdAsync(id);
            return schedule == null ? null : WorkScheduleResponseDTO.FromEntity(schedule);
        }

        public async Task<WorkScheduleResponseDTO?> UpdateAsync(Guid id, WorkScheduleUpdateDTO dto)
        {
            WorkScheduleValidator.ValidateUpdate(dto);

            var schedule = await _repository.GetByIdAsync(id);
            if (schedule == null) return null;

            schedule.Name = dto.Name;
            schedule.Description = dto.Description;
            schedule.Type = (WorkScheduleType)dto.Type;
            schedule.MonitorIdleTime = dto.MonitorIdleTime;
            schedule.MonitorOutsideSchedule = dto.MonitorOutsideSchedule;
            schedule.IsActive = dto.IsActive;
            schedule.UpdatedAt = DateTime.UtcNow;

            schedule.Rules.Clear();

            foreach (var r in dto.Rules)
            {
                schedule.Rules.Add(MapRuleFromDTO(r, schedule.OrganizationId));
            }

            await _repository.UpdateAsync(schedule);
            return WorkScheduleResponseDTO.FromEntity(schedule);
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            return await _repository.DeleteAsync(id);
        }

        public async Task<bool> AssignToUserAsync(AssignUserScheduleDTO dto)
        {
            return await _repository.AssignScheduleToUserAsync(dto.UserId, dto.WorkScheduleId);
        }

        public async Task<WorkScheduleResponseDTO> CloneAsync(Guid id, string newName, Guid organizationId)
        {
            var original = await _repository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Jornada não encontrada.");

            var clone = new WorkSchedule
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = newName,
                Description = original.Description,
                ScheduleCode = original.ScheduleCode,
                Type = original.Type,
                MonitorIdleTime = original.MonitorIdleTime,
                MonitorOutsideSchedule = original.MonitorOutsideSchedule,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Rules = original.Rules.Select(r => new WorkScheduleRule
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    WorkScheduleId = Guid.Empty,
                    Day = r.Day,
                    StartTime = r.StartTime,
                    EndTime = r.EndTime,
                    BreakDuration = r.BreakDuration,
                    ToleranceMinutes = r.ToleranceMinutes,
                    AllowOvertime = r.AllowOvertime,
                    IsActive = r.IsActive,
                    CrossesMidnight = r.CrossesMidnight,
                    BlocksJson = r.BlocksJson
                }).ToList()
            };

            await _repository.CreateAsync(clone);
            return WorkScheduleResponseDTO.FromEntity(clone);
        }

        public async Task<IEnumerable<WorkScheduleUserDTO>> GetUsersAsync(Guid scheduleId)
        {
            return await _repository.GetScheduleUsersAsync(scheduleId);
        }

        public async Task<IEnumerable<WorkScheduleHistoryDTO>> GetUserHistoryAsync(Guid userId, Guid organizationId)
        {
            return await _repository.GetUserScheduleHistoryAsync(userId, organizationId);
        }

        public async Task<WorkScheduleCurrentDTO?> GetCurrentScheduleAsync(Guid userId, Guid organizationId)
        {
            return await _repository.GetCurrentUserScheduleAsync(userId, organizationId);
        }

        private static WorkScheduleRule MapRuleFromDTO(WorkScheduleRuleDTO r, Guid organizationId) =>
            new WorkScheduleRule
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Day = (WorkDay)r.Day,
                StartTime = TimeSpan.FromMinutes(r.StartTimeMinutes),
                EndTime = TimeSpan.FromMinutes(r.EndTimeMinutes),
                BreakDuration = TimeSpan.FromMinutes(r.BreakDurationMinutes),
                ToleranceMinutes = r.ToleranceMinutes,
                AllowOvertime = r.AllowOvertime,
                IsActive = r.IsActive,
                CrossesMidnight = r.CrossesMidnight,
                BlocksJson = r.Blocks == null ? null : JsonSerializer.Serialize(r.Blocks)
            };
    }
}
