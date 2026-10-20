using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Services
{
    public interface IVideoSegmentService
    {
        Task SaveSegmentAsync(VideoSegmentUploadDTO dto);

        /// <summary>URL assinada para o agent subir o MP4 direto ao bucket; null se o bucket estiver desligado.</summary>
        Task<VideoSegmentUploadTicketDTO?> CreateUploadTicketAsync(Guid organizationId);

        /// <summary>Registra (só metadados) o segmento que o agent já subiu ao bucket.</summary>
        Task<VideoSegmentDTO> ConfirmUploadAsync(Guid organizationId, VideoSegmentConfirmDTO dto, string baseUrl);

        Task<VideoSegment?> GetByIdAsync(Guid id);

        Task<VideoSegment?> GetMetadataByIdAsync(Guid id);

        /// <summary>URL assinada de leitura no bucket, para o player tocar direto de lá.</summary>
        Task<string> CreateReadUrlAsync(string storageKey);

        Task<VideoSegmentDTO?> GetLatestAsync(string deviceId, string baseUrl);

        /// <param name="userId">Máquina usada por mais de uma pessoa: só os segmentos desse usuário.</param>
        Task<IEnumerable<VideoSegmentHistoryItemDTO>> GetHistoryWithContextAsync(
            string deviceId, DateTime from, DateTime to, string baseUrl, Guid? userId = null);

        Task<VideoSegment?> GetActivityVideoAsync(string deviceId, DateTime from, DateTime to);

        /// <summary>
        /// Confere a assinatura (exp+sig) anexada pelo BuildSignedUrl. GetById precisa ser
        /// anônimo — a tag &lt;video&gt; do navegador não manda header de autenticação —
        /// então essa é a única barreira contra alguém acessar o vídeo de outra
        /// organização só adivinhando/reaproveitando um GUID.
        /// </summary>
        bool ValidateAccess(Guid id, long exp, string? sig);
    }
}
