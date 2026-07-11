using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Services
{
    public class OrganizationService : IOrganizationService
    {
        private readonly AppDbContext _context;

        public OrganizationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ServiceResult<RegisterOrganizationResultDTO>> RegisterOrganizationAsync(RegisterOrganizationDTO dto)
        {
            // ============================
            // 🔒 Normalização de dados
            // ============================
            var organizationName = dto.OrganizationName.Trim();
            var adminEmail = dto.AdminEmail.Trim().ToLowerInvariant();

            // ============================
            // 🔍 Validações de segurança
            // ============================

            if (await _context.Organizations
                .AsNoTracking()
                .AnyAsync(o => o.Name.ToLower() == organizationName.ToLower()))
            {
                return ServiceResult<RegisterOrganizationResultDTO>.Fail("Não foi possível criar a organização.");
            }

            if (await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Email == adminEmail))
            {
                return ServiceResult<RegisterOrganizationResultDTO>.Fail("Não foi possível criar a organização.");
            }

            // ============================
            // 🔐 Hash seguro da senha
            // ============================
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.AdminPassword, workFactor: 12);

            // ============================
            // 🏢 Criação da Organização
            // ============================
            var organization = new Organization
            {
                Id = Guid.NewGuid(),
                Name = organizationName,
                CreatedAt = DateTime.UtcNow
            };

            // ============================
            // 👤 Criação do Usuário Admin
            // ============================
            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                FirstName = dto.AdminFirstName.Trim(),
                LastName = dto.AdminLastName.Trim(),
                Email = adminEmail,
                PasswordHash = passwordHash,
                Role = "admin",
                OrganizationId = organization.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // ============================
            // 💾 Persistência com transação
            // ============================
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _context.Organizations.Add(organization);
                _context.Users.Add(adminUser);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            // ============================
            // ✅ Retorno seguro
            // ============================
            return ServiceResult<RegisterOrganizationResultDTO>.SuccessResult(new RegisterOrganizationResultDTO
            {
                OrganizationId = organization.Id,
                AdminUserId = adminUser.Id,
                AdminEmail = adminUser.Email
            });
        }
    }
}
