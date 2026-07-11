using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WiseMonitor.Api.DTOs
{
    public class OrganizationWithAdminDTO
    {
        public Guid Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? RazaoSocial { get; set; }
        public string? Cnpj { get; set; }
        public string Dominio { get; set; } = string.Empty;
        public DateTime CriadoEm { get; set; }

        public AdminDTO? Admin { get; set; }

        public class AdminDTO
        {
            public Guid Id { get; set; }
            public string FirstName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
        }
    }
}