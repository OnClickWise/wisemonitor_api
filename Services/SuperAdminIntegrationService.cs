using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs.SuperAdmin;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Services
{
    public class SuperAdminIntegrationService : ISuperAdminIntegrationService
    {
        private readonly AppDbContext _context;

        public SuperAdminIntegrationService(AppDbContext context) => _context = context;

        public async Task<IEnumerable<IntegrationResponseDTO>> GetAllAsync(CancellationToken ct = default)
        {
            return await _context.PlatformIntegrations.AsNoTracking()
                .OrderBy(i => i.Name)
                .Select(i => MapToDTO(i))
                .ToListAsync(ct);
        }

        public async Task<IntegrationResponseDTO> CreateAsync(CreateIntegrationDTO dto, CancellationToken ct = default)
        {
            var integration = new PlatformIntegration
            {
                Type       = dto.Type,
                Name       = dto.Name,
                ConfigJson = JsonSerializer.Serialize(dto.Config),
                IsActive   = dto.IsActive,
                EventsJson = JsonSerializer.Serialize(dto.Events),
            };

            _context.PlatformIntegrations.Add(integration);
            await _context.SaveChangesAsync(ct);

            return MapToDTO(integration);
        }

        public async Task UpdateAsync(Guid id, UpdateIntegrationDTO dto, CancellationToken ct = default)
        {
            var integration = await _context.PlatformIntegrations.FindAsync(new object[] { id }, ct)
                ?? throw new KeyNotFoundException("Integração não encontrada.");

            if (dto.Name != null) integration.Name = dto.Name;
            if (dto.IsActive.HasValue) integration.IsActive = dto.IsActive.Value;
            if (dto.Config != null) integration.ConfigJson = JsonSerializer.Serialize(dto.Config);
            if (dto.Events != null) integration.EventsJson = JsonSerializer.Serialize(dto.Events);

            integration.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var integration = await _context.PlatformIntegrations.FindAsync(new object[] { id }, ct)
                ?? throw new KeyNotFoundException("Integração não encontrada.");

            _context.PlatformIntegrations.Remove(integration);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<IntegrationTestResultDTO> TestConnectivityAsync(Guid id, CancellationToken ct = default)
        {
            var integration = await _context.PlatformIntegrations.FindAsync(new object[] { id }, ct)
                ?? throw new KeyNotFoundException("Integração não encontrada.");

            // Teste básico: verifica se a configuração é um JSON válido e não está vazia
            bool success = false;
            string message;

            try
            {
                var config = JsonSerializer.Deserialize<Dictionary<string, string>>(integration.ConfigJson);
                success = config != null && config.Count > 0;
                message = success ? "Configuração válida." : "Configuração vazia ou inválida.";
            }
            catch (Exception ex)
            {
                message = $"Erro ao ler configuração: {ex.Message}";
            }

            integration.LastTestedAt = DateTime.UtcNow;
            integration.LastTestSuccess = success;
            integration.LastTestMessage = message;
            await _context.SaveChangesAsync(ct);

            return new IntegrationTestResultDTO
            {
                Success  = success,
                Message  = message,
                TestedAt = integration.LastTestedAt.Value,
            };
        }

        private static IntegrationResponseDTO MapToDTO(PlatformIntegration i) => new()
        {
            Id              = i.Id,
            Name            = i.Name,
            Type            = i.Type,
            IsActive        = i.IsActive,
            Events          = JsonSerializer.Deserialize<string[]>(i.EventsJson) ?? [],
            LastTestedAt    = i.LastTestedAt,
            LastTestSuccess = i.LastTestSuccess,
            LastTestMessage = i.LastTestMessage,
            CreatedAt       = i.CreatedAt,
        };
    }
}
