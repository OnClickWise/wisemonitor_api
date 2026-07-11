using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WiseMonitor.Api.Models
{
    public class KeyboardWord
    {
        public Guid Id { get; set; }
        public Guid KeyboardSessionId { get; set; }

        public string? Word { get; set; }
        public int Count { get; set; }

          // ✅ ADICIONAR ESTA LINHA
        public KeyboardSession KeyboardSession { get; set; } = null!;

        public KeyboardWordCategory Category { get; set; }
    }
}