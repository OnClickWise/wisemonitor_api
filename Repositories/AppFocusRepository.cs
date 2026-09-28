using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.Helpers;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Repositories;

public class AppFocusRepository : IAppFocusRepository
{
    private readonly AppDbContext _context;

    public AppFocusRepository(AppDbContext context)
    {
        _context = context;
    }

    // CREATE
    public async Task AddAsync(AppFocusEvent entity)
    {
        _context.AppFocusEvents.Add(entity);
        await _context.SaveChangesAsync();
    }

    // READ - organização + data
    public async Task<IEnumerable<AppFocusEvent>> GetByOrganizationAndPeriodAsync(
    Guid organizationId,
    DateTime startDate,
    DateTime endDate,
    IReadOnlyCollection<Guid>? userIds = null)
    {
        // Dias inteiros: o front manda só datas, e no filtro "dia" start == end
        // virava o intervalo 00:00–00:00 (ver ActivityPeriod).
        var (from, to) = ActivityPeriod.Days(startDate, endDate);

        var query = _context.AppFocusEvents
            .AsNoTracking()
            .Where(e =>
                e.OrganizationId == organizationId &&
                e.StartTime >= from &&
                e.StartTime < to);

        if (userIds != null)
            query = query.Where(e => userIds.Contains(e.UserId));

        return await query
            .OrderBy(e => e.StartTime)
            .ToListAsync();
    }

    // READ - por ID + organização (segurança)
    public async Task<AppFocusEvent?> GetByIdAsync(
        Guid id,
        Guid organizationId)
    {
        return await _context.AppFocusEvents
            .FirstOrDefaultAsync(e =>
                e.Id == id &&
                e.OrganizationId == organizationId);
    }

    // UPDATE
    public async Task UpdateAsync(AppFocusEvent entity)
    {
        _context.AppFocusEvents.Update(entity);
        await _context.SaveChangesAsync();
    }

    // DELETE
    public async Task DeleteAsync(Guid id)
    {
        var entity = await _context.AppFocusEvents.FindAsync(id);
        if (entity == null) return;

        _context.AppFocusEvents.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public Task<IEnumerable<AppFocusEvent>> GetByUserAndDateAsync(Guid userId, DateTime date)
    {
        throw new NotImplementedException();
    }

    public Task<AppFocusEvent?> GetByIdAsync(Guid id)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<AppFocusEvent>> GetByOrganizationAsync(Guid organizationId)
    {
        throw new NotImplementedException();
    }

    public async Task<IEnumerable<AppFocusEvent>> GetByUserDateRangeAsync(Guid userId, DateTime start, DateTime end)
    {
        if (start.Kind == DateTimeKind.Unspecified) start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
            if (end.Kind == DateTimeKind.Unspecified) end = DateTime.SpecifyKind(end, DateTimeKind.Utc);

            // 2. Define intervalo
            var queryStart = start.Date;
            var queryEnd = end.Date.AddDays(1).AddTicks(-1);

            // 3. Busca no Banco de Dados
            return await _context.AppFocusEvents
                .Where(e => e.UserId == userId &&
                            e.StartTime >= queryStart &&
                            e.StartTime <= queryEnd)
                .OrderBy(e => e.StartTime)
                .AsNoTracking()
                .ToListAsync();
    }
}
