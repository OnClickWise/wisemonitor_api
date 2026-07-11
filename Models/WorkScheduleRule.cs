using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WiseMonitor.Api.Models
{
    public class WorkScheduleRule
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // Multi-tenant
        public Guid OrganizationId { get; set; } // Identificador da organização

        // Jornada relacionada
        public Guid WorkScheduleId { get; set; } // Chave estrangeira para WorkSchedule
        public WorkSchedule? WorkSchedule { get; set; } // Propriedade de navegação

        // Dia da semana
        public WorkDay Day { get; set; } // Dia da semana (enum WorkDay)

        // Horários
        public TimeSpan StartTime { get; set; } // Horário de início do expediente
        public TimeSpan EndTime { get; set; } // Horário de término do expediente

        // Intervalo
        public TimeSpan BreakDuration { get; set; } = TimeSpan.FromMinutes(60); // Duração do intervalo

        // Regras
        public int ToleranceMinutes { get; set; } = 5; // Tolerância em minutos para atrasos
        public bool AllowOvertime { get; set; } = false; // Permitir horas extras
        public bool IsActive { get; set; } = true; // Indica se a regra está ativa
        public bool CrossesMidnight { get; set; } = false;
        public string? BlocksJson { get; set; }
    }
}
