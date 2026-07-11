using System;

namespace WiseMonitor.Api.DTOs
{
    /// <summary>
    /// DTO de resposta após upload de screenshot armazenada no banco.
    /// </summary>
    public class LiveMonitoringScreenshotResponseDTO
    {
        /// <summary>
        /// Indica se o upload foi bem-sucedido.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Identificador do usuário monitorado.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Momento em que a screenshot foi capturada.
        /// </summary>
        public DateTime CapturedAt { get; set; }
    
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// URL pública da screenshot (acessada pelo Next.js)
        /// </summary>
        public string ScreenshotUrl { get; set; } = string.Empty;

        /// <summary>
        /// URL da thumbnail (opcional)
        /// </summary>
        public string ThumbnailUrl { get; set; } = string.Empty;
    }
}
