using System;
using System.Collections.Concurrent;
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

    // READ - organização + intervalo [from, to)
    // Sem IconBase64 na consulta principal: o ícone vinha em cada linha (milhares por dia
    // na empresa toda) e a página de atividades passava minutos carregando. O ícone é
    // buscado depois, uma vez por programa.
    public async Task<IEnumerable<AppFocusEvent>> GetByOrganizationAndPeriodAsync(
    Guid organizationId,
    DateTime from,
    DateTime to,
    IReadOnlyCollection<Guid>? userIds = null)
    {
        from = AsUtc(from);
        to = AsUtc(to);

        var query = _context.AppFocusEvents
            .AsNoTracking()
            .Where(e =>
                e.OrganizationId == organizationId &&
                e.StartTime >= from &&
                e.StartTime < to);

        if (userIds != null)
            query = query.Where(e => userIds.Contains(e.UserId));

        return await ToListWithIconsAsync(query.OrderBy(e => e.StartTime));
    }

    // Toda leitura de lista passa por aqui: carrega as colunas sem o IconBase64 (que vinha
    // em cada linha e deixava as consultas em dezenas de segundos ou minutos) e depois
    // coloca o ícone de cada programa, buscado uma vez só.
    private async Task<List<AppFocusEvent>> ToListWithIconsAsync(IQueryable<AppFocusEvent> query, bool withIcons = true)
    {
        var events = await query
            .Select(e => new AppFocusEvent
            {
                Id = e.Id,
                OrganizationId = e.OrganizationId,
                UserId = e.UserId,
                DeviceId = e.DeviceId,
                ApplicationName = e.ApplicationName,
                ProcessName = e.ProcessName,
                WindowTitle = e.WindowTitle,
                Url = e.Url,
                FaviconUrl = e.FaviconUrl,
                StartTime = e.StartTime,
                EndTime = e.EndTime,
                DurationSeconds = e.DurationSeconds,
                Category = e.Category,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync();

        if (withIcons)
            await FillIconsAsync(events);
        return events;
    }

    // Ícone por programa, guardado em memória (não muda entre requisições).
    private static readonly ConcurrentDictionary<(Guid Org, string Process), string?> IconCache = new();

    private async Task FillIconsAsync(List<AppFocusEvent> events)
    {
        if (events.Count == 0)
            return;

        // Todas as consultas desta classe são de uma organização só.
        var organizationId = events[0].OrganizationId;
        var from = events.Min(e => e.StartTime);
        var to = events.Max(e => e.StartTime).AddTicks(1);

        var processes = events
            .Select(e => e.ProcessName)
            .Where(p => !string.IsNullOrEmpty(p))
            .Distinct()
            .ToList();

        foreach (var process in processes)
        {
            if (IconCache.TryGetValue((organizationId, process), out var cached) && cached != null)
                continue;

            // Limitado ao mesmo intervalo, para usar o índice (OrganizationId, StartTime).
            var icon = await _context.AppFocusEvents
                .AsNoTracking()
                .Where(e => e.OrganizationId == organizationId &&
                            e.StartTime >= from &&
                            e.StartTime < to &&
                            e.ProcessName == process &&
                            e.IconBase64 != null && e.IconBase64 != "")
                .Select(e => e.IconBase64)
                .FirstOrDefaultAsync();

            IconCache[(organizationId, process)] = icon;
        }

        foreach (var e in events)
        {
            if (!string.IsNullOrEmpty(e.ProcessName) &&
                IconCache.TryGetValue((organizationId, e.ProcessName), out var icon))
                e.IconBase64 = icon;
        }
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

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
            return await ToListWithIconsAsync(_context.AppFocusEvents
                .AsNoTracking()
                .Where(e => e.UserId == userId &&
                            e.StartTime >= queryStart &&
                            e.StartTime <= queryEnd)
                .OrderBy(e => e.StartTime));
    }

    // Atividades de uma máquina. A página do dispositivo buscava por usuário
    // (deduzido da última captura), e quando a captura era de outra pessoa, ou não
    // havia captura, ou o supervisor não enxergava esse usuário, a lista vinha vazia.
    // OrganizationId + StartTime usa o índice existente; DeviceId filtra o resto.
    public async Task<IEnumerable<AppFocusEvent>> GetByDeviceRangeAsync(
        Guid organizationId, Guid deviceId, DateTime start, DateTime end)
    {
        if (start.Kind == DateTimeKind.Unspecified) start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
        if (end.Kind == DateTimeKind.Unspecified) end = DateTime.SpecifyKind(end, DateTimeKind.Utc);

        return await ToListWithIconsAsync(_context.AppFocusEvents
            .AsNoTracking()
            .Where(e => e.OrganizationId == organizationId &&
                        e.StartTime >= start &&
                        e.StartTime <= end &&
                        e.DeviceId == deviceId)
            .OrderBy(e => e.StartTime));
    }

    // Contexto do histórico de vídeo: só a janela pedida. GetByUserDateRangeAsync
    // arredonda para dias inteiros, e cada busca de 10 min trazia o dia todo.
    // Folga de 1h para trás pega o evento que já estava em foco quando a janela começou.
    public async Task<IEnumerable<AppFocusEvent>> GetByUserOverlappingAsync(
        Guid userId, DateTime start, DateTime end)
    {
        if (start.Kind == DateTimeKind.Unspecified) start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
        if (end.Kind == DateTimeKind.Unspecified) end = DateTime.SpecifyKind(end, DateTimeKind.Utc);

        var lookback = start.AddHours(-1);

        // O contexto do vídeo não usa ícone (VideoSegmentService já zerava).
        return await ToListWithIconsAsync(_context.AppFocusEvents
            .AsNoTracking()
            .Where(e => e.UserId == userId &&
                        e.StartTime >= lookback &&
                        e.StartTime <= end &&
                        (e.EndTime == null || e.EndTime >= start))
            .OrderBy(e => e.StartTime), withIcons: false);
    }
}
