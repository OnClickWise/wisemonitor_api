using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Models.Enums;
using WiseMonitor.Api.Repositories;

namespace WiseMonitor.Api.Services
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _context;
        private readonly ITeamRepository _teamRepo;

        public UserService(AppDbContext context, ITeamRepository teamRepo)
        {
            _context = context;
            _teamRepo = teamRepo;
        }

        public async Task<UserDTO> CreateUserAsync(UserCreateDTO dto, Guid organizationId)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Role = dto.Role,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                OrganizationId = organizationId,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return MapToDTO(user);
        }

        public async Task<bool> DeleteUserAsync(Guid id, Guid organizationId)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id && u.OrganizationId == organizationId);

            if (user == null)
                return false;

            // Soft delete: preserva histórico de monitoramento.
            // Hard delete falha por FK constraints (screenshots, logs, etc.).
            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<UserDTO>> GetAllUsersAsync(Guid organizationId, Guid callerId, string callerRole)
        {
            var query = _context.Users
                .AsNoTracking()
                .Where(u => u.OrganizationId == organizationId);

            // Supervisor só pode ver os usuários das equipes que ele administra —
            // sem isso ele enxergava todo mundo da organização, igual TenantAdmin.
            if (UserRoles.Normalize(callerRole) == UserRoles.Supervisor)
            {
                var memberIds = await _teamRepo.GetManagedMemberIdsAsync(callerId, organizationId);
                query = query.Where(u => memberIds.Contains(u.Id));
            }

            var users = await query.ToListAsync();

            return users.Select(MapToDTO);
        }

        public async Task<UserDTO?> GetUserByIdAsync(Guid id, Guid organizationId)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id && u.OrganizationId == organizationId);

            return user == null ? null : MapToDTO(user);
        }

        public async Task<UserDTO> UpdateUserAsync(Guid userId, UserUpdateDTO dto, Guid organizationId)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId && u.OrganizationId == organizationId);

            if (user == null)
                throw new Exception("Usuário não encontrado");

            user.FirstName = dto.FirstName ?? user.FirstName;
            user.LastName = dto.LastName ?? user.LastName;
            user.Email = dto.Email ?? user.Email;
            user.Role = dto.Role ?? user.Role;
            user.IsActive = dto.IsActive ?? user.IsActive;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return MapToDTO(user);
        }

        private UserDTO MapToDTO(User user)
        {
            return new UserDTO
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role,
                IsActive = user.IsActive,
                AvatarUrl = user.AvatarUrl
            };
        }

        public async Task<UserDTO> UpdateMyAvatarAsync(Guid userId, string? avatarUrl)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                throw new Exception("Usuário não encontrado");

            user.AvatarUrl = avatarUrl?.Trim();
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return MapToDTO(user);
        }
    }
}
