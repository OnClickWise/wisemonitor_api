using System;
using System.Collections.Generic;

namespace WiseMonitor.Api.Models
{
    public class KeyboardSession
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid OrganizationId { get; set; }

        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }

        public string? Application { get; set; }

        public int TotalKeystrokes { get; set; }
        public int LettersCount { get; set; }
        public int WordsCount { get; set; }
        public int NumbersCount { get; set; }
        public int SymbolsCount { get; set; }

        public int ProductivityScore { get; set; }
        public KeyboardClassification Classification { get; set; }

        public ICollection<KeyboardWord> Words { get; set; } = new List<KeyboardWord>();
    }
}