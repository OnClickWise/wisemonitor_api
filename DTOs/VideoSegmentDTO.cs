using System;
using System.Collections.Generic;

namespace WiseMonitor.Api.DTOs
{
    /// <summary>Metadados de um segmento — nunca inclui os bytes do vídeo (usar GET {id} para baixar).</summary>
    public class VideoSegmentDTO
    {
        public Guid Id { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public Guid MonitoredUserId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }
        public string Url { get; set; } = string.Empty;
    }

    /// <summary>Contexto correlacionado a um intervalo: o que estava em foco e o que foi digitado.</summary>
    public class VideoSegmentContextDTO
    {
        public List<AppFocusContextItemDTO> AppFocusEvents { get; set; } = new();
        public List<KeyboardContextItemDTO> KeyboardSessions { get; set; } = new();
    }

    public class AppFocusContextItemDTO
    {
        public string ApplicationName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public string? Url { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }

    public class KeyboardContextItemDTO
    {
        public string? Application { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public int TotalKeystrokes { get; set; }
        public int WordsCount { get; set; }
        public List<string> TopWords { get; set; } = new();
    }

    /// <summary>Um item de histórico: metadados do segmento + o contexto que ocorreu durante ele.</summary>
    public class VideoSegmentHistoryItemDTO
    {
        public VideoSegmentDTO Segment { get; set; } = new();
        public VideoSegmentContextDTO Context { get; set; } = new();
    }
}
