using System;

namespace WiseMonitor.Api.DTOs
{
    public class AssignUserScheduleDTO
    {
        /// <summary>
        /// Usuário a quem o cronograma será atribuído
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Cronograma de trabalho a ser atribuído
        /// </summary>
        public Guid WorkScheduleId { get; set; }

        /// <summary>
        /// Data de início da vigência
        /// </summary>
        public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;

        /// <summary>
        /// Data de término da vigência (opcional)
        /// </summary>
        public DateTime? EndDate { get; set; }

        public string? Reason { get; set; }

        public bool NotifyUser { get; set; } = false;
    }
}
