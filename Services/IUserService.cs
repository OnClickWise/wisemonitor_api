using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;

namespace WiseMonitor.Api.Services
{
    public interface IUserService
    {
        Task<UserDTO> CreateUserAsync(UserCreateDTO dto, Guid organizationId);
        Task<IEnumerable<UserDTO>> GetAllUsersAsync(Guid organizationId);

        Task<UserDTO?> GetUserByIdAsync(Guid id, Guid organizationId);
        Task<bool> DeleteUserAsync(Guid id, Guid organizationId);
        Task<UserDTO> UpdateUserAsync(Guid userId, UserUpdateDTO dto, Guid organizationId);
        Task<UserDTO> UpdateMyAvatarAsync(Guid userId, string? avatarUrl);
    }
}
