using System.ComponentModel.DataAnnotations;
using WiseMonitor.Api.Validators;

namespace WiseMonitor.Api.DTOs
{
    public class RegisterOrganizationDTO
    {
        // ============================
        // 🏢 Dados da Organização
        // ============================

        [Required(ErrorMessage = "O nome da organização é obrigatório.")]
        [MaxLength(100, ErrorMessage = "O nome da organização deve ter no máximo 100 caracteres.")]
        public string OrganizationName { get; set; } = string.Empty;

        // ============================
        // 👤 Dados do Usuário Administrador
        // ============================

        [Required(ErrorMessage = "O nome do administrador é obrigatório.")]
        [MaxLength(100)]
        public string AdminFirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "O sobrenome do administrador é obrigatório.")]
        [MaxLength(100)]
        public string AdminLastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "O e-mail do administrador é obrigatório.")]
        [EmailAddress(ErrorMessage = "E-mail inválido.")]
        public string AdminEmail { get; set; } = string.Empty;

        // 🔐 Senha forte obrigatória
        [Required(ErrorMessage = "A senha é obrigatória.")]
        [MinLength(6, ErrorMessage = "A senha deve ter no mínimo 6 caracteres.")]
        [StrongPassword]
        public string AdminPassword { get; set; } = string.Empty;
    }
}
