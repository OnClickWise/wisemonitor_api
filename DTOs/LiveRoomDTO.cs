using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WiseMonitor.Api.DTOs
{
    /// <summary>
    /// Representa uma sala WebRTC ativa no sistema.
    /// </summary>
    public class LiveRoomDTO
    {
        /// <summary>
        /// Identificador único da sessão/sala.
        /// </summary>
        [JsonPropertyName("sessionId")]
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Quantidade de produtores (desktop apps enviando stream).
        /// </summary>
        [JsonPropertyName("producers")]
        public int Producers { get; set; }

        /// <summary>
        /// Quantidade de visualizadores (usuários assistindo ao stream).
        /// </summary>
        [JsonPropertyName("viewers")]
        public int Viewers { get; set; }

        /// <summary>
        /// Lista de IDs únicos dos usuários conectados.
        /// </summary>
        [JsonPropertyName("users")]
        public List<string> Users { get; set; } = new();
    }
}
