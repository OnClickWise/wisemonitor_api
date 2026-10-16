namespace WiseMonitor.Api.Configs
{
    public class AgentSettings
    {
        // ============================================
        // URL BASE
        // Ex:
        // https://api.wisemonitor.com/downloads/agent
        // ============================================

        public string AppBaseUrl { get; set; }
            = string.Empty;

        public string BaseDownloadUrl { get; set; }
            = string.Empty;

        // URL direta (GitHub LFS, GCS, etc.) — quando definida, bypassa o endpoint /installer
        public string DirectDownloadUrl { get; set; }
            = string.Empty;

        // ============================================
        // VERSÃO ATUAL
        // ============================================

        public string CurrentVersion { get; set; }
            = "1.0.0";

        // ============================================
        // FORCE UPDATE
        // ============================================

        public bool ForceUpdate { get; set; }
            = false;

        // ============================================
        // PLATAFORMA
        // Ex:
        // win-x64
        // linux-x64
        // macos-arm64
        // ============================================

        public string PlatformFolder { get; set; }
            = "win-x64";

        // ============================================
        // NOME DO INSTALADOR
        // ============================================

        public string FileName { get; set; }
            = "WiseMonitorAgentSetup.exe";

        // ============================================
        // SHA-256 DO PACOTE (hex)
        // O agent confere o download contra este valor antes de executar o
        // instalador. Definir a cada release (env AgentSettings__Sha256) —
        // o build-sign.ps1 já imprime o hash do instalador gerado.
        // ============================================

        public string Sha256 { get; set; }
            = string.Empty;
    }
}