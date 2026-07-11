using System;
using System.Collections.Generic;

namespace WiseMonitor.Api.DTOs
{
    public class WorkScheduleRuleDTO
    {
        public Guid Id { get; set; }

        /// <summary>0=Domingo ... 6=Sábado</summary>
        public int Day { get; set; }

        /// <summary>Minutos desde 00:00</summary>
        public int StartTimeMinutes { get; set; }

        /// <summary>Minutos desde 00:00</summary>
        public int EndTimeMinutes { get; set; }

        /// <summary>Duração do intervalo em minutos</summary>
        public int BreakDurationMinutes { get; set; }

        public int ToleranceMinutes { get; set; }

        public bool AllowOvertime { get; set; }

        public bool IsActive { get; set; } = true;

        public bool CrossesMidnight { get; set; } = false;

        public List<WorkScheduleBlockDTO>? Blocks { get; set; }
    }
}
