using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs.Delegation;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Services
{
    public class DelegationService : IDelegationService
    {
        private readonly AppDbContext _context;

        public DelegationService(AppDbContext context) => _context = context;

        public async Task<DelegationDTO> CreateAsync(
            CreateDelegationDTO dto,
            Guid organizationId,
            Guid delegatorId)
        {
            if (dto.EndDate <= dto.StartDate)
                throw new ArgumentException("A data de término deve ser posterior à data de início.");

            if (dto.EndDate <= DateTime.UtcNow)
                throw new ArgumentException("A data de término deve ser no futuro.");

            var delegateUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == dto.DelegateId && u.OrganizationId == organizationId)
                ?? throw new KeyNotFoundException("Usuário delegado não encontrado.");

            var delegation = new Delegation
            {
                OrganizationId = organizationId,
                DelegatorId = delegatorId,
                DelegateId = dto.DelegateId,
                Scope = JsonSerializer.Serialize(dto.Scope),
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                Reason = dto.Reason,
                IsActive = true,
            };

            _context.Delegations.Add(delegation);
            await _context.SaveChangesAsync();

            return await GetDTOByIdAsync(delegation.Id, organizationId);
        }

        public async Task<List<DelegationDTO>> GetAllAsync(Guid organizationId)
        {
            var delegations = await _context.Delegations
                .AsNoTracking()
                .Include(d => d.Delegator)
                .Include(d => d.Delegate)
                .Where(d => d.OrganizationId == organizationId)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            return delegations.Select(MapToDTO).ToList();
        }

        public async Task<List<DelegationDTO>> GetByUserAsync(Guid userId, Guid organizationId)
        {
            var delegations = await _context.Delegations
                .AsNoTracking()
                .Include(d => d.Delegator)
                .Include(d => d.Delegate)
                .Where(d => d.OrganizationId == organizationId &&
                            (d.DelegatorId == userId || d.DelegateId == userId))
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            return delegations.Select(MapToDTO).ToList();
        }

        public async Task RevokeAsync(Guid delegationId, Guid organizationId, Guid revokedBy)
        {
            var delegation = await _context.Delegations
                .FirstOrDefaultAsync(d => d.Id == delegationId && d.OrganizationId == organizationId)
                ?? throw new KeyNotFoundException("Delegação não encontrada.");

            delegation.IsActive = false;
            delegation.RevokedAt = DateTime.UtcNow;
            delegation.RevokedBy = revokedBy;

            await _context.SaveChangesAsync();
        }

        public async Task ExpireOutdatedAsync()
        {
            var expired = await _context.Delegations
                .Where(d => d.IsActive && d.EndDate < DateTime.UtcNow)
                .ToListAsync();

            foreach (var d in expired)
                d.IsActive = false;

            if (expired.Any())
                await _context.SaveChangesAsync();
        }

        private async Task<DelegationDTO> GetDTOByIdAsync(Guid id, Guid organizationId)
        {
            var d = await _context.Delegations
                .AsNoTracking()
                .Include(x => x.Delegator)
                .Include(x => x.Delegate)
                .FirstAsync(x => x.Id == id && x.OrganizationId == organizationId);

            return MapToDTO(d);
        }

        private static DelegationDTO MapToDTO(Delegation d)
        {
            DelegationScopeDTO scope;
            try
            {
                scope = JsonSerializer.Deserialize<DelegationScopeDTO>(d.Scope) ?? new();
            }
            catch
            {
                scope = new();
            }

            return new DelegationDTO
            {
                Id = d.Id,
                OrganizationId = d.OrganizationId,
                DelegatorId = d.DelegatorId,
                DelegatorName = d.Delegator?.FullName ?? string.Empty,
                DelegateId = d.DelegateId,
                DelegateName = d.Delegate?.FullName ?? string.Empty,
                Scope = scope,
                StartDate = d.StartDate,
                EndDate = d.EndDate,
                IsActive = d.IsActive,
                Reason = d.Reason,
                CreatedAt = d.CreatedAt,
            };
        }
    }
}
