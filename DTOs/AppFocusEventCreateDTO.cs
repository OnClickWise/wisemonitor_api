using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WiseMonitor.Api.DTOs
{
    public class AppFocusEventCreateDTO
    {
        public Guid DeviceId { get; set; }

        // Usuário monitorado escolhido na configuração do agent. O token é de quem fez
        // login no agent (normalmente o admin que instalou), não de quem usa a máquina.
        public Guid? UserId { get; set; }
        public string ApplicationName { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        
        // 🌐 NOVOS CAMPOS
        public string? Url { get; set; }
        public string? FaviconUrl { get; set; }
        public string? IconBase64 { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }
}