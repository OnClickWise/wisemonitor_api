using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Repositories;

namespace WiseMonitor.Api.Services
{
    public class ProductivityClassificationService
        : IProductivityClassificationService
    {
        private readonly IProductivityClassificationRepository _repository;
        private readonly ILogger<ProductivityClassificationService> _logger;

        public ProductivityClassificationService(
            IProductivityClassificationRepository repository,
            ILogger<ProductivityClassificationService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<IEnumerable<ProductivityClassificationResponseDTO>>
            GetByTeamAsync(
                Guid organizationId,
                Guid teamId)
        {
            _logger.LogInformation(
                "Buscando classificações de produtividade | Org={OrgId} | Team={TeamId}",
                organizationId,
                teamId);

            var items = await _repository.GetByTeamAsync(
                organizationId,
                teamId);

            return items.Select(x => new ProductivityClassificationResponseDTO
            {
                Identifier = x.Identifier,
                DisplayName = x.DisplayName,
                ItemType = x.ItemType,
                Category = x.Category
            });
        }

        public async Task SaveAsync(
            Guid organizationId,
            Guid teamId,
            ProductivityClassificationSaveDTO dto)
        {
            _logger.LogInformation(
                "Salvando classificações | Org={OrgId} | Team={TeamId} | Total={Count}",
                organizationId,
                teamId,
                dto.Items.Count);

            var entities = dto.Items.Select(item => new ProductivityClassification
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                TeamId = teamId,

                Identifier = item.Identifier,
                DisplayName = item.DisplayName,

                ItemType = item.ItemType,
                Category = item.Category,

                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await _repository.SaveAsync(entities);

            _logger.LogInformation("Classificações salvas com sucesso.");
        }
    }
}
