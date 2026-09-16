using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Repositories;

namespace WiseMonitor.Api.Services
{
    public class MouseService : IMouseService
    {
        private readonly IMouseRepository _repository;

        public MouseService(IMouseRepository repository)
        {
            _repository = repository;
        }

        public async Task ProcessMouseEventAsync(
            MouseEventCreateDTO dto,
            Guid userId,
            Guid organizationId)
        {
            var metrics = dto.Metrics ?? new MouseMetricsDTO();

            var session = new MouseSession
            {
                Id = dto.SessionId,
                UserId = userId,
                OrganizationId = organizationId,
                StartAt = dto.StartAt,
                EndAt = dto.EndAt,
                Application = dto.Application,
                LeftClicks = metrics.LeftClicks,
                RightClicks = metrics.RightClicks,
                MiddleClicks = metrics.MiddleClicks,
                ScrollCount = metrics.ScrollCount
            };

            await _repository.CreateAsync(session);
        }

        public Task<MouseSession> GetByIdAsync(Guid id, Guid userId)
            => _repository.GetByIdAsync(id, userId);

        public Task<IEnumerable<MouseSession>> GetHistoryAsync(
            Guid userId, DateTime start, DateTime end)
            => _repository.GetHistoryAsync(userId, start, end);

        public Task<MouseSummaryDTO> GetSummaryAsync(
            Guid userId, DateTime start, DateTime end)
            => _repository.GetSummaryAsync(userId, start, end);

        public async Task<bool> UpdateAsync(
            Guid id, MouseEventUpdateDTO dto, Guid userId)
        {
            var session = await _repository.GetByIdAsync(id, userId);
            if (session == null) return false;

            session.EndAt = dto.EndAt;
            session.LeftClicks = dto.LeftClicks;
            session.RightClicks = dto.RightClicks;
            session.MiddleClicks = dto.MiddleClicks;
            session.ScrollCount = dto.ScrollCount;

            await _repository.UpdateAsync(session);
            return true;
        }

        public async Task<bool> DeleteAsync(Guid id, Guid userId)
        {
            var session = await _repository.GetByIdAsync(id, userId);
            if (session == null) return false;

            await _repository.DeleteAsync(session);
            return true;
        }
    }
}
