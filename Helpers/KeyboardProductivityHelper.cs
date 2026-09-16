using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Helpers
{
    public static class KeyboardProductivityHelper
    {
        public static (int Score, KeyboardClassification Classification) Calculate(
            KeyboardEventCreateDTO dto)
        {
            var metrics = dto.Metrics ?? new KeyboardMetricsDTO();

            var score =
                (metrics.Words * 2) +
                (metrics.Letters * 0.1) -
                (metrics.Symbols * 0.5);

            var classification =
                score >= 70 ? KeyboardClassification.Produtivo :
                score >= 40 ? KeyboardClassification.Neutro :
                              KeyboardClassification.Improdutivo;

            return ((int)score, classification);
        }

        public static (double WordsPerMinute, double CorrectionRate) CalculateQuality(
            KeyboardEventCreateDTO dto, double durationMinutes)
        {
            var metrics = dto.Metrics ?? new KeyboardMetricsDTO();

            var wpm = durationMinutes > 0 ? metrics.Words / durationMinutes : 0;
            var correctionRate = metrics.TotalKeystrokes > 0
                ? (double)metrics.BackspaceCount / metrics.TotalKeystrokes
                : 0;

            return (Math.Round(wpm, 2), Math.Round(correctionRate, 4));
        }
    }
}