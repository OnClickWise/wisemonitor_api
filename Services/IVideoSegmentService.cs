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
        Task<VideoSegment?> GetByIdAsync(Guid id);
        Task<VideoSegmentDTO?> GetLatestAsync(string deviceId, string baseUrl);
        Task<IEnumerable<VideoSegmentHistoryItemDTO>> GetHistoryWithContextAsync(
            string deviceId, DateTime from, DateTime to, string baseUrl);

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
