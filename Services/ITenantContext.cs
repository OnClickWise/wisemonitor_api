using System;

namespace WiseMonitor.Api.Services
{
    /// <summary>
    /// Expõe a organização do usuário autenticado na requisição atual, para os
    /// global query filters de multi-tenant no <c>AppDbContext</c>.
    /// </summary>
    public interface ITenantContext
    {
        Guid? OrganizationId { get; }
        bool IsSuperAdmin { get; }

        /// <summary>
        /// Falso fora de um request HTTP real (testes, migrations, design-time).
        /// Quando falso, os query filters de tenant não são aplicados.
        /// </summary>
        bool IsActive { get; }
    }
}
