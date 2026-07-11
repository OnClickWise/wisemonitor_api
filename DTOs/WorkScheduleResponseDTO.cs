using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.DTOs
{
    public class WorkScheduleResponseDTO
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ScheduleCode { get; set; }
        public int Type { get; set; }
        public string TypeLabel { get; set; } = string.Empty;
        public bool MonitorOutsideSchedule { get; set; }
        public bool MonitorIdleTime { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<WorkScheduleRuleDTO> Rules { get; set; } = new();

        public static WorkScheduleResponseDTO FromEntity(WorkSchedule entity)
        {
            return new WorkScheduleResponseDTO
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                ScheduleCode = entity.ScheduleCode,
                Type = (int)entity.Type,
                TypeLabel = GetTypeLabel(entity.Type),
                MonitorOutsideSchedule = entity.MonitorOutsideSchedule,
                MonitorIdleTime = entity.MonitorIdleTime,
                IsActive = entity.IsActive,
                CreatedAt = entity.CreatedAt,
                Rules = entity.Rules.Select(r => new WorkScheduleRuleDTO
                {
                    Id = r.Id,
                    Day = (int)r.Day,
                    StartTimeMinutes = (int)r.StartTime.TotalMinutes,
                    EndTimeMinutes = (int)r.EndTime.TotalMinutes,
                    BreakDurationMinutes = (int)r.BreakDuration.TotalMinutes,
                    ToleranceMinutes = r.ToleranceMinutes,
                    AllowOvertime = r.AllowOvertime,
                    IsActive = r.IsActive,
                    CrossesMidnight = r.CrossesMidnight,
                    Blocks = string.IsNullOrEmpty(r.BlocksJson)
                        ? null
                        : JsonSerializer.Deserialize<List<WorkScheduleBlockDTO>>(r.BlocksJson)
                }).ToList()
            };
        }

        private static string GetTypeLabel(WorkScheduleType type) => type switch
        {
            WorkScheduleType.Fixed => "Horário Fixo",
            WorkScheduleType.FiveTwo => "5x2",
            WorkScheduleType.SixOne => "6x1",
            WorkScheduleType.TwelveThirtySix => "12x36",
            WorkScheduleType.Supervision => "Supervisão",
            WorkScheduleType.Custom => "Personalizado",
            _ => "Desconhecido"
        };
    }
}
