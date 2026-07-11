using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace WiseMonitor.Api.Validators
{
    public class StrongPasswordAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(
            object? value,
            ValidationContext validationContext)
        {
            var password = value as string;

            if (string.IsNullOrWhiteSpace(password))
                return new ValidationResult("A senha é obrigatória.");

            if (password.Length < 6)
                return new ValidationResult("A senha deve ter no mínimo 6 caracteres.");

            // Regras:
            // ✔ Pelo menos 1 letra
            // ✔ Pelo menos 1 número
            // ✔ Pelo menos 1 caractere especial
            var regex = new Regex(
                @"^(?=.*[A-Za-z])(?=.*\d)(?=.*[^A-Za-z\d]).+$");

            if (!regex.IsMatch(password))
            {
                return new ValidationResult(
                    "A senha deve conter letras, números e caracteres especiais.");
            }

            return ValidationResult.Success;
        }
    }
}
