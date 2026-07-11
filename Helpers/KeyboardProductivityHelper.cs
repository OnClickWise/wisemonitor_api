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
            var score =
                (dto.Metrics.Words * 2) +
                (dto.Metrics.Letters * 0.1) -
                (dto.Metrics.Symbols * 0.5);

            var classification =
                score >= 70 ? KeyboardClassification.Produtivo :
                score >= 40 ? KeyboardClassification.Neutro :
                              KeyboardClassification.Improdutivo;

            return ((int)score, classification);
        }
    }
}