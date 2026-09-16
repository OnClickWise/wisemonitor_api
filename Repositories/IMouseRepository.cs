using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Repositories
{
    public interface IMouseRepository
    {
        Task CreateAsync(MouseSession session);
        Task<MouseSession> GetByIdAsync(Guid id, Guid userId);

        Task<IEnumerable<MouseSession>> GetHistoryAsync(
            Guid userId, DateTime start, DateTime end);

        Task<MouseSummaryDTO> GetSummaryAsync(
            Guid userId, DateTime start, DateTime end);

        Task UpdateAsync(MouseSession session);
        Task DeleteAsync(MouseSession session);
    }
}
