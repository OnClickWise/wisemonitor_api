using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models; 
namespace WiseMonitor.Api.Services;

public interface IAppFocusService
{
    // CREATE
    Task RegisterEventAsync(
        AppFocusEventCreateDTO dto,
        Guid userId,
        Guid organizationId);

    // READ
    Task<IEnumerable<AppFocusEventResponseDTO>> GetAllAsync(
        Guid organizationId,
        DateTime startDate,
        DateTime endDate);

    Task<AppFocusEventResponseDTO?> GetByIdAsync(
        Guid id,
        Guid organizationId);

    // UPDATE
    Task UpdateAsync(
        Guid id,
        AppFocusEventUpdateDTO dto,
        Guid organizationId);

    // DELETE
    Task DeleteAsync(
        Guid id,
        Guid organizationId);

    // METRICS
    Task<IEnumerable<object>> GetMetricsAsync(
        Guid userId,
        DateTime date);

    // HISTORIC
    Task<IEnumerable<AppFocusEvent>> GetHistoryAsync(
        Guid userId, 
        DateTime start, 
        DateTime end);
}
