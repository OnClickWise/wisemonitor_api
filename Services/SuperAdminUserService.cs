using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.DTOs.SuperAdmin;
using WiseMonitor.Api.Models.Enums;
using WiseMonitor.Api.Utils;

namespace WiseMonitor.Api.Services
{
    public class SuperAdminUserService : ISuperAdminUserService
    {
        private readonly AppDbContext _context;
        private readonly IAuditService _audit;

        public SuperAdminUserService(AppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<PagedResult<SuperAdminUserResponseDTO>> GetAllAsync(SuperAdminUserFilterDTO filter, CancellationToken ct = default)
        {
            var query = _context.Users.AsNoTracking()
                .Include(u => u.Organization)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var s = filter.Search.ToLower();
                query = query.Where(u =>
                    u.Email.ToLower().Contains(s) ||
                    u.FirstName.ToLower().Contains(s) ||
                    u.LastName.ToLower().Contains(s));
            }

            if (filter.OrganizationId.HasValue)
                query = query.Where(u => u.OrganizationId == filter.OrganizationId);

            if (!string.IsNullOrWhiteSpace(filter.Role))
                query = query.Where(u => u.Role == filter.Role);

            if (filter.IsActive.HasValue)
                query = query.Where(u => u.IsActive == filter.IsActive.Value);

            var total = await query.CountAsync(ct);

            var items = await query
                .OrderBy(u => u.Email)
                .Skip((filter.Page - 1) * filter.Limit)
                .Take(filter.Limit)
                .Select(u => new SuperAdminUserResponseDTO
                {
                    Id = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    Role = u.Role,
                    IsActive = u.IsActive,
                    IsSuperAdmin = u.IsSuperAdmin,
                    OrganizationId = u.OrganizationId,
                    OrganizationName = u.Organization != null ? u.Organization.Name : null,
                    CreatedAt = u.CreatedAt,
                })
                .ToListAsync(ct);

            return new PagedResult<SuperAdminUserResponseDTO>
            {
                Items = items,
                Meta = new PaginationMeta { Page = filter.Page, Limit = filter.Limit, Total = total }
            };
        }

        public async Task<SuperAdminUserResponseDTO> CreateSuperAdminAsync(CreateSuperAdminDTO dto, Guid creatorId, CancellationToken ct = default)
        {
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email, ct))
                throw new InvalidOperationException("E-mail já está em uso.");

            var user = new Models.User
            {
                FirstName = dto.FirstName,
                LastName  = dto.LastName,
                Email     = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role        = UserRoles.SuperAdmin,
                IsSuperAdmin = true,
                IsActive    = true,
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync(ct);

            await _audit.LogAsync(
                action:     "superadmin_create",
                entityType: "User",
                entityId:   user.Id.ToString(),
                details:    $"SuperAdmin '{user.Email}' criado.",
                userId:     creatorId,
                userRole:   UserRoles.SuperAdmin);

            return new SuperAdminUserResponseDTO
            {
                Id          = user.Id,
                FirstName   = user.FirstName,
                LastName    = user.LastName,
                Email       = user.Email,
                Role        = user.Role,
                IsActive    = user.IsActive,
                IsSuperAdmin = user.IsSuperAdmin,
                CreatedAt   = user.CreatedAt,
            };
        }

        public async Task<SuperAdminUserResponseDTO?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return await _context.Users.AsNoTracking()
                .Include(u => u.Organization)
                .Where(u => u.Id == id)
                .Select(u => new SuperAdminUserResponseDTO
                {
                    Id = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    Role = u.Role,
                    IsActive = u.IsActive,
                    IsSuperAdmin = u.IsSuperAdmin,
                    OrganizationId = u.OrganizationId,
                    OrganizationName = u.Organization != null ? u.Organization.Name : null,
                    CreatedAt = u.CreatedAt,
                })
                .FirstOrDefaultAsync(ct);
        }

        public async Task UpdateAsync(Guid id, UserUpdateDTO dto, CancellationToken ct = default)
        {
            var user = await _context.Users.FindAsync(new object[] { id }, ct)
                ?? throw new KeyNotFoundException("Usuário não encontrado.");

            if (dto.FirstName != null) user.FirstName = dto.FirstName;
            if (dto.LastName != null) user.LastName = dto.LastName;
            if (!string.IsNullOrWhiteSpace(dto.Role))
                user.Role = UserRoles.Normalize(dto.Role);
            if (dto.IsActive.HasValue)
                user.IsActive = dto.IsActive.Value;

            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(Guid id, Guid adminId, CancellationToken ct = default)
        {
            var user = await _context.Users.FindAsync(new object[] { id }, ct)
                ?? throw new KeyNotFoundException("Usuário não encontrado.");

            if (user.IsSuperAdmin)
                throw new InvalidOperationException("Não é possível excluir um SuperAdmin.");

            await _audit.LogAsync(
                action: "user_delete",
                entityType: "User",
                entityId: id.ToString(),
                details: $"Usuário '{user.Email}' excluído por SuperAdmin.",
                userId: adminId,
                userRole: UserRoles.SuperAdmin);

            _context.Users.Remove(user);
            await _context.SaveChangesAsync(ct);
        }

        public async Task InvalidateAllSessionsAsync(Guid id, CancellationToken ct = default)
        {
            // Remove sessões ativas do usuário na tabela LiveSessions
            var sessions = await _context.LiveSessions
                .Where(s => s.OrganizationId == _context.Users
                    .Where(u => u.Id == id)
                    .Select(u => u.OrganizationId)
                    .FirstOrDefault())
                .ToListAsync(ct);

            // Sem tabela de sessões individuais por usuário, apenas logamos a ação
            await _audit.LogAsync(
                action: "user_force_logout",
                entityType: "User",
                entityId: id.ToString(),
                details: "Todas as sessões invalidadas por SuperAdmin.",
                userId: id,
                userRole: UserRoles.SuperAdmin);
        }

        public async Task UnlockAsync(Guid id, Guid adminId, CancellationToken ct = default)
        {
            var user = await _context.Users.FindAsync(new object[] { id }, ct)
                ?? throw new KeyNotFoundException("Usuário não encontrado.");

            user.IsActive = true;
            await _context.SaveChangesAsync(ct);

            await _audit.LogAsync(
                action: "user_unlock",
                entityType: "User",
                entityId: id.ToString(),
                details: $"Usuário '{user.Email}' desbloqueado por SuperAdmin.",
                userId: adminId,
                userRole: UserRoles.SuperAdmin);
        }

        public async Task<IEnumerable<object>> GetActiveSessionsAsync(Guid id, CancellationToken ct = default)
        {
            var user = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id, ct);

            if (user?.OrganizationId == null) return [];

            var sessions = await _context.LiveSessions.AsNoTracking()
                .Where(s => s.OrganizationId == user.OrganizationId)
                .Select(s => new { s.Id, s.OrganizationId, s.CreatedAt })
                .ToListAsync(ct);

            return sessions.Cast<object>();
        }
    }
}
