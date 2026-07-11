using System.Collections.Generic;

namespace WiseMonitor.Api.DTOs
{
    public class WorkScheduleUpdateDTO
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
        public bool MonitorOutsideSchedule { get; set; }

        /// <summary>
        /// Monitorar tempo ocioso
        /// </summary>
        public bool MonitorIdleTime { get; set; }

        /// <summary>
        /// Indica se o cronograma está ativo
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Regras do cronograma (substitui todas)
        /// </summary>
        public List<WorkScheduleRuleDTO> Rules { get; set; } = new();
    }
}
