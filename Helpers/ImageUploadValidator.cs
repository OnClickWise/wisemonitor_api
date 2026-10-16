using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace WiseMonitor.Api.Helpers
{
    /// <summary>
    /// Avatar e logo são gravados em wwwroot e servidos pela própria API. Sem validação,
    /// dava para subir um .html/.svg com script e ter XSS armazenado no domínio da API.
    /// Confere tamanho, extensão e a assinatura binária (não confia no Content-Type nem
    /// no nome do arquivo) e devolve a extensão canônica a ser usada no disco.
    /// </summary>
    public static class ImageUploadValidator
    {
        public const long MaxBytes = 2 * 1024 * 1024;

        public static async Task<(bool Ok, string? Extension, string? Error)> ValidateAsync(IFormFile file)
        {
            if (file.Length == 0)
                return (false, null, "Arquivo vazio.");

            if (file.Length > MaxBytes)
                return (false, null, "A imagem deve ter no máximo 2 MB.");

            var header = new byte[12];
            await using (var stream = file.OpenReadStream())
            {
                var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
                if (read < 4)
                    return (false, null, "Arquivo de imagem inválido.");
            }

            var detected = Detect(header);
            if (detected == null)
                return (false, null, "Formato não suportado. Use PNG, JPG, WEBP ou GIF.");

            return (true, detected, null);
        }

        /// <summary>
        /// URL informada à mão no lugar do arquivo: só caminho do próprio /uploads ou https.
        /// Bloqueia javascript:, data: e afins.
        /// </summary>
        public static bool IsSafeImageUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return true;

            if (url.StartsWith("/uploads/", StringComparison.Ordinal) && !url.Contains(".."))
                return true;

            return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
        }

        private static string? Detect(byte[] h)
        {
            if (h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47) return ".png";
            if (h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return ".jpg";
            if (h[0] == 0x47 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x38) return ".gif";
            if (h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46 &&
                h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50) return ".webp";
            return null;
        }
    }
}
