using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Repositories;

public interface IAppFocusRepository
{
    Task AddAsync(AppFocusEvent entity);

    Task<IEnumerable<AppFocusEvent>> GetByUserAndDateAsync(
        Guid userId,
        DateTime date);

    // userIds null = organização inteira
    Task<IEnumerable<AppFocusEvent>> GetByOrganizationAndPeriodAsync(
        Guid organizationId,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyCollection<Guid>? userIds = null);

    Task<AppFocusEvent?> GetByIdAsync(
        Guid id,
        Guid organizationId);

    Task UpdateAsync(AppFocusEvent entity);

    Task DeleteAsync(Guid id);

    Task<IEnumerable<AppFocusEvent>> GetByUserDateRangeAsync(
        Guid userId, 
        DateTime start, 
        DateTime end);
}
