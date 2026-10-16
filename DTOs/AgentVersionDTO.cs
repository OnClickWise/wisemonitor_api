namespace WiseMonitor.Api.DTOs
{
    public class AgentVersionDTO
    {
        // ============================================
        // VERSÃO ATUAL
        // ============================================

        public string Version { get; set; }
            = string.Empty;

        // ============================================
        // FORCE UPDATE
        // ============================================

        public bool ForceUpdate { get; set; }

        // ============================================
        // URL DOWNLOAD
        // ============================================

        public string DownloadUrl { get; set; }
            = string.Empty;

        // SHA-256 (hex) do pacote — vazio quando não configurado.
        public string Sha256 { get; set; }
            = string.Empty;
    }
}