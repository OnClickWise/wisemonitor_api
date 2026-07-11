using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WiseMonitor.Api.DTOs
{
    public class KeyboardEventUpdateDTO
    {
        public DateTime EndAt { get; set; }

        public int TotalKeystrokes { get; set; }
        public int LettersCount { get; set; }
        public int WordsCount { get; set; }
        public int NumbersCount { get; set; }
        public int SymbolsCount { get; set; }
    }
}