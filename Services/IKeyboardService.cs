using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Services
{
    public interface IKeyboardService
    {
        Task ProcessKeyboardEventAsync(
            KeyboardEventCreateDTO dto,
            Guid userId,
            Guid organizationId);

        Task<KeyboardSession> GetByIdAsync(Guid id, Guid userId);

        Task<IEnumerable<KeyboardSession>> GetHistoryAsync(
            Guid userId, DateTime start, DateTime end);

        Task<KeyboardSummaryDTO> GetSummaryAsync(
            Guid userId, DateTime start, DateTime end);

        Task<bool> UpdateAsync(
            Guid id, KeyboardEventUpdateDTO dto, Guid userId);

        Task<bool> DeleteAsync(Guid id, Guid userId);
    }
}