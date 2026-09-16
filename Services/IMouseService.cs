using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Services
{
    public interface IMouseService
    {
        Task ProcessMouseEventAsync(
            MouseEventCreateDTO dto,
            Guid userId,
            Guid organizationId);

        Task<MouseSession> GetByIdAsync(Guid id, Guid userId);

        Task<IEnumerable<MouseSession>> GetHistoryAsync(
            Guid userId, DateTime start, DateTime end);

        Task<MouseSummaryDTO> GetSummaryAsync(
            Guid userId, DateTime start, DateTime end);

        Task<bool> UpdateAsync(
            Guid id, MouseEventUpdateDTO dto, Guid userId);

        Task<bool> DeleteAsync(Guid id, Guid userId);
    }
}
