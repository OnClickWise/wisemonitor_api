using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.Extensions;
using WiseMonitor.Api.Models.Enums;

namespace WiseMonitor.Api.Services
{
    /// <summary>
    /// Até onde o usuário logado enxerga dentro da própria organização.
    /// Supervisor fica restrito às equipes que administra (responsável ou administrador):
    /// os membros delas e ele mesmo. Os demais papéis enxergam a organização inteira —
    /// o que cada um pode FAZER continua sendo decidido pela RolePermissionMatrix.
    /// </summary>
    public sealed class AccessScope
    {
        public Guid OrganizationId { get; }
        public Guid UserId { get; }
        public bool IsRestricted { get; }
        public IReadOnlySet<Guid> UserIds { get; }
        public IReadOnlySet<Guid> TeamIds { get; }

        public AccessScope(Guid organizationId, Guid userId, bool isRestricted,
            IReadOnlySet<Guid> userIds, IReadOnlySet<Guid> teamIds)
        {
            OrganizationId = organizationId;
            UserId = userId;
            IsRestricted = isRestricted;
            UserIds = userIds;
            TeamIds = teamIds;
        }

        public bool CanAccessUser(Guid userId) => !IsRestricted || UserIds.Contains(userId);
        public bool CanAccessTeam(Guid teamId) => !IsRestricted || TeamIds.Contains(teamId);
    }

    public enum ScheduleAccess
    {
        /// <summary>Não existe na organização do caller.</summary>
        NotFound,
        /// <summary>Existe, mas o caller não pode nem ver.</summary>
        Hidden,
        /// <summary>Pode ver, atribuir às próprias equipes e duplicar, mas não alterar.</summary>
        Read,
        /// <summary>Pode editar e apagar.</summary>
        Manage
    }

    public interface IAccessScopeService
    {
        Task<AccessScope> GetScopeAsync(ClaimsPrincipal user);

        /// <summary>Usuário existe na organização do caller e está dentro do escopo dele.</summary>
        Task<bool> CanAccessUserAsync(ClaimsPrincipal user, Guid targetUserId);

        /// <summary>
        /// Usuário monitorado numa máquina: o atribuído manualmente por um admin tem
        /// prioridade sobre o da última captura (mesma regra da tela de dispositivos).
        /// </summary>
        Task<Guid?> ResolveDeviceUserIdAsync(string deviceId, Guid organizationId);

        Task<bool> CanAccessDeviceAsync(ClaimsPrincipal user, string deviceId);

        /// <summary>deviceId → usuário atribuído manualmente, para filtrar listas numa consulta só.</summary>
        Task<IReadOnlyDictionary<string, Guid>> GetAssignedDeviceUsersAsync(Guid organizationId);

        Task<ScheduleAccess> GetScheduleAccessAsync(ClaimsPrincipal user, Guid scheduleId);

        /// <summary>Acesso a cada jornada da organização do caller (as ocultas ficam de fora).</summary>
        Task<IReadOnlyDictionary<Guid, ScheduleAccess>> GetVisibleSchedulesAsync(ClaimsPrincipal user);
    }

    public class AccessScopeService : IAccessScopeService
    {
        private readonly AppDbContext _context;

        // Scoped: um cálculo por request, mesmo com várias checagens no mesmo endpoint.
        private AccessScope? _cached;
        private Guid _cachedFor;

        public AccessScopeService(AppDbContext context)
        {
            _context = context;
        }

        public static bool IsRestrictedRole(string? role) =>
            UserRoles.Normalize(role) == UserRoles.Supervisor;

        public async Task<AccessScope> GetScopeAsync(ClaimsPrincipal user)
        {
            var userId = user.GetUserId();
            var orgId = user.GetOrganizationId();

            if (_cached != null && _cachedFor == userId && _cached.OrganizationId == orgId)
                return _cached;

            var role = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value;

            AccessScope scope;
            if (!IsRestrictedRole(role))
            {
                scope = new AccessScope(orgId, userId, isRestricted: false,
                    new HashSet<Guid>(), new HashSet<Guid>());
            }
            else
            {
                var teams = await _context.Teams
                    .AsNoTracking()
                    .Where(t => t.OrganizationId == orgId &&
                                (t.ManagerId == userId ||
                                 t.Members.Any(m => m.UserId == userId && m.IsManager)))
                    .Select(t => new
                    {
                        t.Id,
                        t.ManagerId,
                        MemberIds = t.Members.Select(m => m.UserId).ToList()
                    })
                    .ToListAsync();

                var userIds = teams
                    .SelectMany(t => t.MemberIds.Append(t.ManagerId))
                    .Append(userId)
                    .ToHashSet();

                scope = new AccessScope(orgId, userId, isRestricted: true,
                    userIds, teams.Select(t => t.Id).ToHashSet());
            }

            _cached = scope;
            _cachedFor = userId;
            return scope;
        }

        public async Task<bool> CanAccessUserAsync(ClaimsPrincipal user, Guid targetUserId)
        {
            var scope = await GetScopeAsync(user);
            if (scope.OrganizationId == Guid.Empty || !scope.CanAccessUser(targetUserId))
                return false;

            return await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == targetUserId && u.OrganizationId == scope.OrganizationId);
        }

        public async Task<Guid?> ResolveDeviceUserIdAsync(string deviceId, Guid organizationId)
        {
            if (Guid.TryParse(deviceId, out var deviceGuid))
            {
                var assigned = await _context.Devices
                    .AsNoTracking()
                    .Where(d => d.Id == deviceGuid && d.OrganizationId == organizationId)
                    .Select(d => d.UserId)
                    .FirstOrDefaultAsync();

                if (assigned.HasValue)
                    return assigned;
            }

            var fromScreenshot = await _context.Screenshots
                .AsNoTracking()
                .Where(s => s.OrganizationId == organizationId && s.DeviceId == deviceId)
                .OrderByDescending(s => s.CapturedAt)
                .Select(s => (Guid?)s.MonitoredUserId)
                .FirstOrDefaultAsync();

            return fromScreenshot;
        }

        public async Task<bool> CanAccessDeviceAsync(ClaimsPrincipal user, string deviceId)
        {
            var scope = await GetScopeAsync(user);
            if (!scope.IsRestricted)
                return true;

            var deviceUserId = await ResolveDeviceUserIdAsync(deviceId, scope.OrganizationId);
            return deviceUserId.HasValue && scope.CanAccessUser(deviceUserId.Value);
        }

        public async Task<IReadOnlyDictionary<string, Guid>> GetAssignedDeviceUsersAsync(Guid organizationId)
        {
            var devices = await _context.Devices
                .AsNoTracking()
                .Where(d => d.OrganizationId == organizationId && d.UserId != null)
                .Select(d => new { d.Id, d.UserId })
                .ToListAsync();

            return devices.ToDictionary(d => d.Id.ToString(), d => d.UserId!.Value);
        }

        // ============================================================
        // JORNADAS
        // ============================================================
        // Supervisor:
        //  - vê as jornadas das próprias equipes/usuários, as que criou e as que ainda
        //    não estão em uso por ninguém (modelos, como as padrão);
        //  - edita/apaga as que criou ou que estão em uso SÓ dentro do escopo dele —
        //    alterar uma jornada usada por outra equipe mudaria o horário dela também.

        public async Task<ScheduleAccess> GetScheduleAccessAsync(ClaimsPrincipal user, Guid scheduleId)
        {
            var scope = await GetScopeAsync(user);

            var schedule = await _context.WorkSchedules
                .AsNoTracking()
                .Where(w => w.Id == scheduleId && w.OrganizationId == scope.OrganizationId)
                .Select(w => new { w.Id, w.CreatedByUserId })
                .FirstOrDefaultAsync();

            if (schedule == null)
                return ScheduleAccess.NotFound;

            if (!scope.IsRestricted)
                return ScheduleAccess.Manage;

            var links = await LoadScheduleLinksAsync(scope.OrganizationId, scheduleId);
            return Classify(scope, schedule.CreatedByUserId, links.GetValueOrDefault(scheduleId));
        }

        public async Task<IReadOnlyDictionary<Guid, ScheduleAccess>> GetVisibleSchedulesAsync(ClaimsPrincipal user)
        {
            var scope = await GetScopeAsync(user);

            var schedules = await _context.WorkSchedules
                .AsNoTracking()
                .Where(w => w.OrganizationId == scope.OrganizationId)
                .Select(w => new { w.Id, w.CreatedByUserId })
                .ToListAsync();

            if (!scope.IsRestricted)
                return schedules.ToDictionary(s => s.Id, _ => ScheduleAccess.Manage);

            var links = await LoadScheduleLinksAsync(scope.OrganizationId, scheduleId: null);

            return schedules
                .Select(s => (s.Id, Access: Classify(scope, s.CreatedByUserId, links.GetValueOrDefault(s.Id))))
                .Where(x => x.Access != ScheduleAccess.Hidden)
                .ToDictionary(x => x.Id, x => x.Access);
        }

        private sealed record ScheduleLinks(List<Guid> TeamIds, List<Guid> UserIds);

        private async Task<Dictionary<Guid, ScheduleLinks>> LoadScheduleLinksAsync(Guid organizationId, Guid? scheduleId)
        {
            var teamLinks = await _context.Teams
                .AsNoTracking()
                .Where(t => t.OrganizationId == organizationId && t.DefaultWorkScheduleId != null &&
                            (scheduleId == null || t.DefaultWorkScheduleId == scheduleId))
                .Select(t => new { ScheduleId = t.DefaultWorkScheduleId!.Value, t.Id })
                .ToListAsync();

            // Vínculos ativos. As atribuições antigas não filtram por org (bug corrigido
            // em AssignScheduleToUserAsync), então o filtro de org vem pelo usuário.
            var userLinks = await _context.UserWorkSchedules
                .AsNoTracking()
                .Where(us => us.IsActive &&
                             _context.Users.Any(u => u.Id == us.UserId && u.OrganizationId == organizationId) &&
                             (scheduleId == null || us.WorkScheduleId == scheduleId))
                .Select(us => new { us.WorkScheduleId, us.UserId })
                .ToListAsync();

            var result = new Dictionary<Guid, ScheduleLinks>();
            ScheduleLinks For(Guid id) =>
                result.TryGetValue(id, out var l) ? l : result[id] = new ScheduleLinks(new(), new());

            foreach (var t in teamLinks) For(t.ScheduleId).TeamIds.Add(t.Id);
            foreach (var u in userLinks) For(u.WorkScheduleId).UserIds.Add(u.UserId);

            return result;
        }

        private static ScheduleAccess Classify(AccessScope scope, Guid? createdBy, ScheduleLinks? links)
        {
            var teamIds = links?.TeamIds ?? new List<Guid>();
            var userIds = links?.UserIds ?? new List<Guid>();

            var createdByMe = createdBy == scope.UserId;
            var usedInScope = teamIds.Any(scope.TeamIds.Contains) || userIds.Any(scope.UserIds.Contains);
            var usedOutsideScope = teamIds.Any(id => !scope.TeamIds.Contains(id)) ||
                                   userIds.Any(id => !scope.UserIds.Contains(id));
            var unused = teamIds.Count == 0 && userIds.Count == 0;

            if ((createdByMe || usedInScope) && !usedOutsideScope)
                return ScheduleAccess.Manage;

            if (createdByMe || usedInScope || unused)
                return ScheduleAccess.Read;

            return ScheduleAccess.Hidden;
        }
    }
}
