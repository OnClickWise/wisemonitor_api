using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Repositories
{
    public interface IKeyboardRepository
    {
        Task CreateAsync(KeyboardSession session);
        Task<KeyboardSession> GetByIdAsync(Guid id, Guid userId);

        Task<IEnumerable<KeyboardSession>> GetHistoryAsync(
            Guid userId, DateTime start, DateTime end);

        // userIds null = organização inteira
        Task<IEnumerable<KeyboardSession>> GetByOrganizationAsync(
            Guid organizationId, DateTime start, DateTime end, IReadOnlyCollection<Guid>? userIds);
        Task<KeyboardSummaryDTO> GetSummaryAsync(
            Guid userId, DateTime start, DateTime end);

        Task UpdateAsync(KeyboardSession session);
        Task DeleteAsync(KeyboardSession session);
    }
}