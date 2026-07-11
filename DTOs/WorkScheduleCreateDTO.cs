using System.Collections.Generic;

namespace WiseMonitor.Api.DTOs
{
    public class WorkScheduleCreateDTO
    {
        /// <summary>
        /// Nome do cronograma
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Descrição do cronograma
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Tipo do cronograma (WorkScheduleType)
        /// </summary>
        public int Type { get; set; }

        /// <summary>
        /// Monitorar atividades fora do cronograma
        /// </summary>
        public bool MonitorOutsideSchedule { get; set; } = true;

        /// <summary>
        /// Monitorar tempo ocioso
        /// </summary>
        public bool MonitorIdleTime { get; set; } = true;

        public string? ScheduleCode { get; set; }

        public Guid? DepartmentId { get; set; }

        /// <summary>
        /// Regras do cronograma
        /// </summary>
        public List<WorkScheduleRuleDTO> Rules { get; set; } = new();
    }
}
