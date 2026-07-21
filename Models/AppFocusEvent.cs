using System;

namespace WiseMonitor.Api.Models;

public class AppFocusEvent
{
    public Guid Id { get; set; }

    // Multi-tenant
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public Guid DeviceId { get; set; }

     // Aplicação
    public string ApplicationName { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;

    // NOVOS CAMPOS (WEB)
    public string? Url { get; set; }              // https://chat.openai.com
    public string? FaviconUrl { get; set; }       // https://site.com/favicon.ico
    public string? IconBase64 { get; set; }       // Ícone do aplicativo em Base64

    // Tempo
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public long DurationSeconds { get; set; }

    // Classificação
    public ActivityCategory Category { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
