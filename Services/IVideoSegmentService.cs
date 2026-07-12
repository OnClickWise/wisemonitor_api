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
    }
}
