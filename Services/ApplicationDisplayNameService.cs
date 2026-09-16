using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Repositories;

namespace WiseMonitor.Api.Services
{
    public class ApplicationDisplayNameService
        : IApplicationDisplayNameService
    {
        private readonly IApplicationDisplayNameRepository _repository;

        public ApplicationDisplayNameService(
            IApplicationDisplayNameRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<ApplicationDisplayName>> GetAllAsync(
            Guid organizationId)
        {
            return await _repository.GetByOrganizationAsync(
                organizationId);
        }

        public async Task<ApplicationDisplayName> SaveAsync(
            Guid organizationId,
            string applicationName,
            string displayName)
        {
            if (organizationId == Guid.Empty)
            {
                throw new Exception("Organização inválida.");
            }

            if (string.IsNullOrWhiteSpace(applicationName))
            {
                throw new Exception("Nome do aplicativo é obrigatório.");
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new Exception("Nome exibido é obrigatório.");
            }

            return await _repository.SaveAsync(
                organizationId,
                applicationName.Trim(),
                displayName.Trim());
        }
    }
}
