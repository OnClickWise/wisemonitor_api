using System;

using System.Text.Json.Serialization;


namespace WiseMonitor.Api.DTOs
{
    /// <summary>
    /// Representa um ICE candidate enviado pelo WebRTC.
    /// </summary>
    public class IceCandidateDTO
    {
        // Linha do candidate (ICE line)
        [JsonPropertyName("candidate")]
        public string? Candidate { get; set; }
        
        // Identificador da mídia (m-line) dentro do SDP
        [JsonPropertyName("sdpMid")]
        public string? SdpMid { get; set; }

        // Índice da linha no SDP
        [JsonPropertyName("sdpMLineIndex")]
        public int? SdpMlineIndex { get; set; }
    }

    /// <summary>
    /// Representa uma mensagem de sinalização WebRTC (offer, answer, candidate, hello, etc.).
    /// </summary>
    public class WebRtcSignalDTO
    {
        // Tipo da mensagem: "hello", "offer", "answer", "ice-candidate", "ping", etc.
        [JsonPropertyName("type")]
        public string? Type { get; set; }



        // Sessão SDP (para offer/answer)
        [JsonPropertyName("sdp")]
        public string? Sdp { get; set; }

        // Objeto de candidate (para mensagens "ice-candidate")
        [JsonPropertyName("candidate")]
        public IceCandidateDTO? Candidate { get; set; }

        // Papel do cliente ("producer" ou "viewer")
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        // ID da sessão compartilhada entre Producer e Viewer
        [JsonPropertyName("sessionId")]
        public string? SessionId { get; set; }

        // Campo adicionado pelo middleware ("producer" ou "viewer")
        [JsonPropertyName("from")]
        public string? From { get; set; }

        // Token opcional de autenticação
        [JsonPropertyName("token")]
        public string? Token { get; set; }

        // ID opcional do dispositivo (desktop)
        [JsonPropertyName("deviceId")]
        public string? DeviceId { get; set; }

        // ID opcional do usuário (produtor ou viewer)
        [JsonPropertyName("userId")]
        public string? UserId { get; set; }

        // Campo opcional para destino específico
        [JsonPropertyName("to")]
        public string? To { get; set; }
    }
}
