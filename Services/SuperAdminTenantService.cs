using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs.SuperAdmin;
using WiseMonitor.Api.Helpers;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Models.Enums;
using WiseMonitor.Api.Utils;

namespace WiseMonitor.Api.Services
{
    public class SuperAdminTenantService : ISuperAdminTenantService
    {
        private readonly AppDbContext _context;
        private readonly IAuditService _audit;
        private readonly IJwtService _jwt;

        public SuperAdminTenantService(AppDbContext context, IAuditService audit, IJwtService jwt)
        {
            _context = context;
            _audit = audit;
            _jwt = jwt;
        }

        public async Task<PagedResult<TenantDTO>> GetAllAsync(TenantFilterDTO filter, CancellationToken ct = default)
        {
            var query = _context.Organizations
                .AsNoTracking()
                .Include(o => o.AdminUser)
                .Include(o => o.Users)
                .Include(o => o.Departments)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.ToLower();
                query = query.Where(o =>
                    o.Name.ToLower().Contains(search) ||
                    (o.AdminUser != null && o.AdminUser.Email.ToLower().Contains(search)));
            }

            if (!string.IsNullOrWhiteSpace(filter.Plan))
                query = query.Where(o => o.Plan == filter.Plan);

            if (!string.IsNullOrWhiteSpace(filter.Status))
                query = query.Where(o => o.Status == filter.Status);

            if (filter.CreatedFrom.HasValue)
                query = query.Where(o => o.CreatedAt >= filter.CreatedFrom.Value);

            if (filter.CreatedTo.HasValue)
                query = query.Where(o => o.CreatedAt <= filter.CreatedTo.Value);

            query = (filter.SortBy, filter.Order.ToLower()) switch
            {
                ("Name", "asc")       => query.OrderBy(o => o.Name),
                ("Name", _)           => query.OrderByDescending(o => o.Name),
                ("Plan", "asc")       => query.OrderBy(o => o.Plan),
                ("Plan", _)           => query.OrderByDescending(o => o.Plan),
                ("UserCount", "asc")  => query.OrderBy(o => o.CachedUserCount),
                ("UserCount", _)      => query.OrderByDescending(o => o.CachedUserCount),
                ("LastActivity", _)   => query.OrderByDescending(o => o.LastActivityAt),
                ("CreatedAt", "asc")  => query.OrderBy(o => o.CreatedAt),
                _                     => query.OrderByDescending(o => o.CreatedAt),
            };

            var total = await query.CountAsync(ct);

            var items = await query
                .Skip((filter.Page - 1) * filter.Limit)
                .Take(filter.Limit)
                .ToListAsync(ct);

            var teamCounts = await _context.Teams
                .GroupBy(t => t.OrganizationId)
                .Select(g => new { OrgId = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var result = items.Select(o => new TenantDTO
            {
                Id = o.Id,
                Name = o.Name,
                Plan = o.Plan,
                Status = o.Status,
                UserCount = o.Users.Count,
                DepartmentCount = o.Departments.Count,
                TeamCount = teamCounts.FirstOrDefault(t => t.OrgId == o.Id)?.Count ?? 0,
                AdminEmail = o.AdminUser?.Email,
                AdminName = o.AdminUser != null ? $"{o.AdminUser.FirstName} {o.AdminUser.LastName}".Trim() : null,
                BillingEmail = o.BillingEmail,
                InternalNotes = o.InternalNotes,
                MaxUsers = o.MaxUsers,
                MaxDevices = o.MaxDevices,
                StorageLimitGb = o.StorageLimitGb,
                TrialEndsAt = o.TrialEndsAt,
                CreatedAt = o.CreatedAt,
                SuspendedAt = o.SuspendedAt,
                SuspendReason = o.SuspendReason,
                SuspensionType = o.SuspensionType,
                SuspendUntil = o.SuspendUntil,
                LastActivityAt = o.LastActivityAt,
                Branding = MapBranding(o),
            }).ToList();

            return new PagedResult<TenantDTO>
            {
                Items = result,
                Meta = new PaginationMeta { Page = filter.Page, Limit = filter.Limit, Total = total }
            };
        }

        public async Task<TenantDTO?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var o = await _context.Organizations
                .AsNoTracking()
                .Include(o => o.AdminUser)
                .Include(o => o.Users)
                .Include(o => o.Departments)
                .FirstOrDefaultAsync(o => o.Id == id, ct);

            if (o == null) return null;

            return new TenantDTO
            {
                Id = o.Id,
                Name = o.Name,
                Plan = o.Plan,
                Status = o.Status,
                UserCount = o.Users.Count,
                DepartmentCount = o.Departments.Count,
                TeamCount = await _context.Teams.CountAsync(t => t.OrganizationId == o.Id, ct),
                AdminEmail = o.AdminUser?.Email,
                AdminName = o.AdminUser != null ? $"{o.AdminUser.FirstName} {o.AdminUser.LastName}".Trim() : null,
                BillingEmail = o.BillingEmail,
                InternalNotes = o.InternalNotes,
                MaxUsers = o.MaxUsers,
                MaxDevices = o.MaxDevices,
                StorageLimitGb = o.StorageLimitGb,
                TrialEndsAt = o.TrialEndsAt,
                CreatedAt = o.CreatedAt,
                SuspendedAt = o.SuspendedAt,
                SuspendReason = o.SuspendReason,
                SuspensionType = o.SuspensionType,
                SuspendUntil = o.SuspendUntil,
                LastActivityAt = o.LastActivityAt,
                Branding = MapBranding(o),
            };
        }

        public async Task<TenantDTO> CreateAsync(CreateTenantDTO dto, Guid createdByAdminId, CancellationToken ct = default)
        {
            if (await _context.Users.AnyAsync(u => u.Email == dto.AdminEmail, ct))
                throw new InvalidOperationException($"Já existe um usuário com o e-mail '{dto.AdminEmail}'.");

            var org = new Organization
            {
                Name = dto.OrganizationName,
                Plan = dto.Plan,
                Status = dto.TrialEndsAt.HasValue ? "Trial" : "Active",
                MaxUsers = dto.MaxUsers,
                StorageLimitGb = dto.StorageLimitGb,
                TrialEndsAt = dto.TrialEndsAt,
                InternalNotes = dto.InternalNotes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            _context.Organizations.Add(org);
            await _context.SaveChangesAsync(ct);

            var admin = new User
            {
                OrganizationId = org.Id,
                FirstName = dto.AdminFirstName,
                LastName = dto.AdminLastName,
                Email = dto.AdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.AdminPassword),
                Role = UserRoles.TenantAdmin,
                IsActive = true,
            };

            _context.Users.Add(admin);
            await _context.SaveChangesAsync(ct);

            org.AdminUserId = admin.Id;
            await _context.SaveChangesAsync(ct);

            await _audit.LogAsync(
                action: "tenant_create",
                entityType: "Organization",
                entityId: org.Id.ToString(),
                details: $"Tenant '{org.Name}' criado por SuperAdmin",
                userId: createdByAdminId,
                userRole: UserRoles.SuperAdmin);

            return (await GetByIdAsync(org.Id, ct))!;
        }

        public async Task UpdateAsync(Guid id, UpdateTenantDTO dto, CancellationToken ct = default)
        {
            var org = await _context.Organizations.FindAsync(new object[] { id }, ct)
                ?? throw new KeyNotFoundException("Tenant não encontrado.");

            if (dto.Name != null) org.Name = dto.Name;
            if (dto.Plan != null) org.Plan = dto.Plan;
            if (dto.MaxUsers.HasValue) org.MaxUsers = dto.MaxUsers;
            if (dto.MaxDevices.HasValue) org.MaxDevices = dto.MaxDevices;
            if (dto.StorageLimitGb.HasValue) org.StorageLimitGb = dto.StorageLimitGb;
            if (dto.InternalNotes != null) org.InternalNotes = dto.InternalNotes;
            if (dto.BillingEmail != null) org.BillingEmail = dto.BillingEmail;
            if (dto.TrialEndsAt.HasValue) org.TrialEndsAt = dto.TrialEndsAt;

            org.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(Guid id, string reason, Guid adminId, CancellationToken ct = default)
        {
            var org = await _context.Organizations.FindAsync(new object[] { id }, ct)
                ?? throw new KeyNotFoundException("Tenant não encontrado.");

            await _audit.LogAsync(
                action: "tenant_delete",
                entityType: "Organization",
                entityId: id.ToString(),
                details: $"Tenant '{org.Name}' excluído. Motivo: {reason}",
                userId: adminId,
                userRole: UserRoles.SuperAdmin);

            _context.Organizations.Remove(org);
            await _context.SaveChangesAsync(ct);
        }

        public async Task SuspendAsync(Guid id, SuspendTenantDTO dto, Guid adminId, CancellationToken ct = default)
        {
            var org = await _context.Organizations.FindAsync(new object[] { id }, ct)
                ?? throw new KeyNotFoundException("Tenant não encontrado.");

            if (org.Status == "Suspended")
                throw new InvalidOperationException("Tenant já está suspenso.");

            org.Status = "Suspended";
            org.SuspendedAt = DateTime.UtcNow;
            org.SuspendReason = dto.Reason;
            org.SuspensionType = dto.Type;
            org.SuspendUntil = dto.SuspendUntil;
            org.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            await _audit.LogAsync(
                action: "tenant_suspend",
                entityType: "Organization",
                entityId: id.ToString(),
                details: $"Tenant '{org.Name}' suspenso. Motivo: {dto.Reason} | Tipo: {dto.Type}",
                userId: adminId,
                userRole: UserRoles.SuperAdmin);
        }

        public async Task ActivateAsync(Guid id, Guid adminId, CancellationToken ct = default)
        {
            var org = await _context.Organizations.FindAsync(new object[] { id }, ct)
                ?? throw new KeyNotFoundException("Tenant não encontrado.");

            org.Status = "Active";
            org.SuspendedAt = null;
            org.SuspendReason = null;
            org.SuspensionType = null;
            org.SuspendUntil = null;
            org.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            await _audit.LogAsync(
                action: "tenant_activate",
                entityType: "Organization",
                entityId: id.ToString(),
                details: $"Tenant '{org.Name}' reativado.",
                userId: adminId,
                userRole: UserRoles.SuperAdmin);
        }

        public async Task UpdatePlanAsync(Guid id, UpdatePlanDTO dto, Guid adminId, CancellationToken ct = default)
        {
            var org = await _context.Organizations.FindAsync(new object[] { id }, ct)
                ?? throw new KeyNotFoundException("Tenant não encontrado.");

            var oldPlan = org.Plan;
            org.Plan = dto.Plan;
            org.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            await _audit.LogAsync(
                action: "tenant_plan_change",
                entityType: "Organization",
                entityId: id.ToString(),
                oldValue: oldPlan,
                newValue: dto.Plan,
                details: dto.BillingNote,
                userId: adminId,
                userRole: UserRoles.SuperAdmin);
        }

        public async Task<TenantStatsDTO?> GetStatsAsync(Guid id, CancellationToken ct = default)
        {
            var org = await _context.Organizations.AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id, ct);

            if (org == null) return null;

            var userCount = await _context.Users.CountAsync(u => u.OrganizationId == id, ct);
            var activeUsers = await _context.Users.CountAsync(u => u.OrganizationId == id && u.IsActive, ct);
            var deviceCount = await _context.Devices.CountAsync(d => d.OrganizationId == id, ct);
            var onlineDevices = await _context.Devices.CountAsync(d => d.OrganizationId == id && d.IsOnline, ct);
            var screenshots = await _context.Screenshots.CountAsync(s => s.OrganizationId == id, ct);
            var teams = await _context.Teams.CountAsync(t => t.OrganizationId == id, ct);
            var depts = await _context.Departments.CountAsync(d => d.OrganizationId == id, ct);

            return new TenantStatsDTO
            {
                OrganizationId = id,
                Name = org.Name,
                TotalUsers = userCount,
                ActiveUsers = activeUsers,
                TotalDevices = deviceCount,
                OnlineDevices = onlineDevices,
                TotalScreenshots = screenshots,
                TotalTeams = teams,
                TotalDepartments = depts,
                LastActivityAt = org.LastActivityAt,
            };
        }

        public async Task<PagedResult<SuperAdminUserResponseDTO>> GetUsersAsync(Guid tenantId, PaginationDTO pagination, CancellationToken ct = default)
        {
            var query = _context.Users.AsNoTracking()
                .Where(u => u.OrganizationId == tenantId);

            var total = await query.CountAsync(ct);

            var items = await query
                .OrderBy(u => u.FirstName)
                .Skip((pagination.Page - 1) * pagination.Limit)
                .Take(pagination.Limit)
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
                    CreatedAt = u.CreatedAt,
                })
                .ToListAsync(ct);

            return new PagedResult<SuperAdminUserResponseDTO>
            {
                Items = items,
                Meta = new PaginationMeta { Page = pagination.Page, Limit = pagination.Limit, Total = total }
            };
        }

        public async Task<string> ResetAdminPasswordAsync(Guid tenantId, Guid adminId, CancellationToken ct = default)
        {
            var org = await _context.Organizations
                .Include(o => o.AdminUser)
                .FirstOrDefaultAsync(o => o.Id == tenantId, ct)
                ?? throw new KeyNotFoundException("Tenant não encontrado.");

            if (org.AdminUser == null)
                throw new InvalidOperationException("Tenant não possui admin configurado.");

            var tempPassword = Guid.NewGuid().ToString("N")[..12];
            org.AdminUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);
            await _context.SaveChangesAsync(ct);

            await _audit.LogAsync(
                action: "tenant_reset_admin_password",
                entityType: "Organization",
                entityId: tenantId.ToString(),
                details: $"Senha do admin do tenant '{org.Name}' redefinida por SuperAdmin.",
                userId: adminId,
                userRole: UserRoles.SuperAdmin);

            return tempPassword;
        }

        public async Task<string> GenerateImpersonationTokenAsync(Guid tenantId, Guid adminId, CancellationToken ct = default)
        {
            var org = await _context.Organizations
                .Include(o => o.AdminUser)
                .FirstOrDefaultAsync(o => o.Id == tenantId, ct)
                ?? throw new KeyNotFoundException("Tenant não encontrado.");

            if (org.AdminUser == null)
                throw new InvalidOperationException("Tenant não possui admin configurado.");

            var token = _jwt.GenerateToken(org.AdminUser);

            await _audit.LogAsync(
                action: "tenant_impersonate",
                entityType: "Organization",
                entityId: tenantId.ToString(),
                details: $"SuperAdmin gerou token de impersonação para tenant '{org.Name}'",
                userId: adminId,
                userRole: UserRoles.SuperAdmin);

            return token;
        }

        public async Task<BrandingDTO?> GetBrandingAsync(Guid tenantId, CancellationToken ct = default)
        {
            var org = await _context.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == tenantId, ct);
            return org == null ? null : MapBranding(org);
        }

        public async Task UpdateBrandingAsync(Guid tenantId, UpdateBrandingDTO dto, CancellationToken ct = default)
        {
            var org = await _context.Organizations.FindAsync(new object[] { tenantId }, ct)
                ?? throw new KeyNotFoundException("Tenant não encontrado.");

            org.BrandingLogoUrl       = dto.LogoUrl       ?? org.BrandingLogoUrl;
            org.BrandingDisplayName   = dto.DisplayName   ?? org.BrandingDisplayName;
            org.BrandingPrimaryColor  = dto.PrimaryColor  ?? org.BrandingPrimaryColor;
            org.BrandingSecondaryColor = dto.SecondaryColor ?? org.BrandingSecondaryColor;
            org.BrandingAccentColor   = dto.AccentColor   ?? org.BrandingAccentColor;
            org.BrandingFontFamily    = dto.FontFamily     ?? org.BrandingFontFamily;
            org.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
        }

        private static BrandingDTO? MapBranding(Organization o)
        {
            if (o.BrandingLogoUrl == null && o.BrandingDisplayName == null &&
                o.BrandingPrimaryColor == null && o.BrandingSecondaryColor == null &&
                o.BrandingAccentColor == null && o.BrandingFontFamily == null)
                return null;

            return new BrandingDTO
            {
                LogoUrl        = o.BrandingLogoUrl,
                DisplayName    = o.BrandingDisplayName,
                PrimaryColor   = o.BrandingPrimaryColor,
                SecondaryColor = o.BrandingSecondaryColor,
                AccentColor    = o.BrandingAccentColor,
                FontFamily     = o.BrandingFontFamily,
            };
        }
    }
}
