using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WiseMonitor.Api.Models
{
    public class UserWorkSchedule
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // Multi-tenant
        public Guid OrganizationId { get; set; }

        // Usuário
        public Guid UserId { get; set; }
        public User? User { get; set; } // Navegação para o usuário

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow; // Data de atribuição da jornada

        // Jornada
        public Guid WorkScheduleId { get; set; } // Chave estrangeira para WorkSchedule
        public WorkSchedule? WorkSchedule { get; set; } // Navegação para a jornada de trabalho

        // Vigência
        public DateTime StartDate { get; set; } = DateTime.UtcNow.Date; // Data de início da vigência
        public DateTime? EndDate { get; set; } // Data de término da vigência (opcional)

        public bool IsActive { get; set; } = true; // Indica se a associação está ativa
        public string? Reason { get; set; }
        public bool NotifyUser { get; set; } = false;
    }
}
