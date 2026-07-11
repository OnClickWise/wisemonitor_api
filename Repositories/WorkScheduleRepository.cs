using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Repositories
{
    public class WorkScheduleRepository : IWorkScheduleRepository
    {
        private readonly AppDbContext _context;

        public WorkScheduleRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(WorkSchedule schedule)
        {
            _context.WorkSchedules.Add(schedule);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(WorkSchedule schedule)
        {
            _context.WorkSchedules.Update(schedule);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var schedule = await _context.WorkSchedules.FindAsync(id);
            if (schedule == null) return false;

            _context.WorkSchedules.Remove(schedule);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<WorkSchedule?> GetByIdAsync(Guid id)
        {
            return await _context.WorkSchedules
                .Include(w => w.Rules)
                .FirstOrDefaultAsync(w => w.Id == id);
        }

        public async Task<IEnumerable<WorkSchedule>> GetAllByOrganizationAsync(Guid organizationId)
        {
            return await _context.WorkSchedules
                .Include(w => w.Rules)
                .Where(w => w.OrganizationId == organizationId)
                .ToListAsync();
        }

        public async Task<bool> AssignScheduleToUserAsync(Guid userId, Guid scheduleId)
        {
            var existingSchedules = _context.UserWorkSchedules
                .Where(x => x.UserId == userId);

            _context.UserWorkSchedules.RemoveRange(existingSchedules);

            var userSchedule = new UserWorkSchedule
            {
                UserId = userId,
                WorkScheduleId = scheduleId
            };

            _context.UserWorkSchedules.Add(userSchedule);

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<WorkScheduleUserDTO>> GetScheduleUsersAsync(Guid scheduleId)
        {
            return await _context.UserWorkSchedules
                .AsNoTracking()
                .Include(us => us.User)
                .Where(us => us.WorkScheduleId == scheduleId)
                .Select(us => new WorkScheduleUserDTO
                {
                    UserId = us.UserId,
                    FullName = us.User != null ? us.User.FullName : string.Empty,
                    Email = us.User != null ? us.User.Email : string.Empty,
                    StartDate = us.StartDate,
                    EndDate = us.EndDate,
                    IsActive = us.IsActive
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<WorkScheduleHistoryDTO>> GetUserScheduleHistoryAsync(Guid userId, Guid organizationId)
        {
            return await _context.UserWorkSchedules
                .AsNoTracking()
                .Include(us => us.WorkSchedule)
                .Where(us => us.UserId == userId && us.OrganizationId == organizationId)
                .OrderByDescending(us => us.StartDate)
                .Select(us => new WorkScheduleHistoryDTO
                {
                    WorkScheduleId = us.WorkScheduleId,
                    WorkScheduleName = us.WorkSchedule != null ? us.WorkSchedule.Name : string.Empty,
                    TypeLabel = us.WorkSchedule != null ? GetTypeLabel(us.WorkSchedule.Type) : string.Empty,
                    StartDate = us.StartDate,
                    EndDate = us.EndDate,
                    IsActive = us.IsActive
                })
                .ToListAsync();
        }

        public async Task<WorkScheduleCurrentDTO?> GetCurrentUserScheduleAsync(Guid userId, Guid organizationId)
        {
            var today = DateTime.UtcNow.Date;
            var todayDay = (int)DateTime.UtcNow.DayOfWeek == 0 ? 7 : (int)DateTime.UtcNow.DayOfWeek;

            var assignment = await _context.UserWorkSchedules
                .AsNoTracking()
                .Include(us => us.WorkSchedule).ThenInclude(ws => ws!.Rules)
                .Where(us => us.UserId == userId
                    && us.OrganizationId == organizationId
                    && us.IsActive
                    && us.StartDate <= today
                    && (us.EndDate == null || us.EndDate >= today))
                .OrderByDescending(us => us.StartDate)
                .FirstOrDefaultAsync();

            if (assignment?.WorkSchedule == null) return null;

            var ws = assignment.WorkSchedule;
            var todayRule = ws.Rules.FirstOrDefault(r => (int)r.Day == todayDay);

            return new WorkScheduleCurrentDTO
            {
                WorkScheduleId = ws.Id,
                WorkScheduleName = ws.Name,
                TypeLabel = GetTypeLabel(ws.Type),
                TodayRule = todayRule == null ? null : new DTOs.WorkScheduleRuleDTO
                {
                    Id = todayRule.Id,
                    Day = (int)todayRule.Day,
                    StartTimeMinutes = (int)todayRule.StartTime.TotalMinutes,
                    EndTimeMinutes = (int)todayRule.EndTime.TotalMinutes,
                    BreakDurationMinutes = (int)todayRule.BreakDuration.TotalMinutes,
                    ToleranceMinutes = todayRule.ToleranceMinutes,
                    AllowOvertime = todayRule.AllowOvertime,
                    IsActive = todayRule.IsActive,
                    CrossesMidnight = todayRule.CrossesMidnight
                }
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
