using WiseMonitor.Api.Validators;

namespace WiseMonitor.Api.DTOs
{

    public class ResetPasswordRequestDTO
    {
        public string Token { get; set; } = string.Empty;

        [StrongPassword]
        public string NewPassword { get; set; } = string.Empty;
    }
}
