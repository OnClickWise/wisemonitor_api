using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Services
{
    public interface IAuthService
    {
        Task<LoginResponseDTO> LoginAsync(LoginRequestDTO LoginDto);
        string HashPassword(string password);
        bool VerifyPassword(string password, string hashedPassword);
    }
}
