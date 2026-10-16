using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace WiseMonitor.Api.Middlewares
{
    /// <summary>
    /// Cabeçalhos de defesa em toda resposta da API. A API só serve JSON, imagens/vídeos
    /// e arquivos enviados (avatar/logo em /uploads) — nada disso precisa rodar script,
    /// então a CSP é "nega tudo": se alguém conseguir subir um HTML/SVG, ele não executa.
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

        public Task InvokeAsync(HttpContext context)
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Cross-Origin-Resource-Policy"] = "cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

            if (!context.Request.Path.StartsWithSegments("/swagger"))
                headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; sandbox";

            return _next(context);
        }
    }
}
