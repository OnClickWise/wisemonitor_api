using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WiseMonitor.Api.Models
{
    public class WorkSchedule
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // Multi-tenant
        public Guid OrganizationId { get; set; } 

        // Dados básicos
        public string Name { get; set; } = string.Empty; // Nome do cronograma
        public string? Description { get; set; } // Descrição opcional
        public string? ScheduleCode { get; set; }

        public WorkScheduleType Type { get; set; } = WorkScheduleType.Fixed; // Tipo de cronograma

        // Monitoramento
        public bool MonitorOutsideSchedule { get; set; } = true; // Monitorar atividades fora do cronograma
        public bool MonitorIdleTime { get; set; } = true; // Monitorar tempo ocioso

        // Relacionamentos
        public ICollection<WorkScheduleRule> Rules { get; set; } // Regras associadas ao cronograma
            = new List<WorkScheduleRule>();

        public ICollection<UserWorkSchedule> UserSchedules { get; set; } // Usuários associados ao cronograma
            = new List<UserWorkSchedule>();

        // Auditoria
        public bool IsActive { get; set; } = true; // Indica se o cronograma está ativo
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Data de criação
        public DateTime? UpdatedAt { get; set; } // Data da última atualização
    }
}
