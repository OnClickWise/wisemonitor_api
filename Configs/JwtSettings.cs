using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System;

namespace WiseMonitor.Api.Config
{
    public class JwtSettings
{
    public string SecretKey { get; set; } = string.Empty; 
    public string? Issuer { get; set; }
    public string? Audience { get; set; }
    public int ExpirationMinutes { get; set; }
}

}
