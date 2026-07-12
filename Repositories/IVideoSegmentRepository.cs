using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Repositories
{
    public interface IVideoSegmentRepository
    {
        Task<VideoSegment?> GetByIdAsync(Guid id);
        Task<VideoSegment?> GetLatestAsync(string deviceId);
        Task<IEnumerable<VideoSegment>> GetHistoryAsync(string deviceId, DateTime from, DateTime to);

        /// <summary>Insere o segmento e remove, para o mesmo device, os segmentos mais antigos que a janela de retenção.</summary>
        Task UpsertAsync(VideoSegment segment, TimeSpan retentionWindow);
    }
}
