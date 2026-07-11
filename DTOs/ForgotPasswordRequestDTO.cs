using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WiseMonitor.Api.DTOs
{
    public class ForgotPasswordRequestDTO
    {
        public string Email { get; set; } = string.Empty;
    }
}
