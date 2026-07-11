namespace WiseMonitor.Api.DTOs
{
    public class AgentDownloadDTO
    {
        // ============================================
        // URL COMPLETA DOWNLOAD
        // ============================================

        public string Url { get; set; }
            = string.Empty;

        // ============================================
        // VERSÃO
        // ============================================

        public string Version { get; set; }
            = string.Empty;

        // ============================================
        // NOME ARQUIVO
        // ============================================

        public string FileName { get; set; }
            = string.Empty;

        // ============================================
        // PLATAFORMA
        // ============================================

        public string Platform { get; set; }
            = string.Empty;
    }
}