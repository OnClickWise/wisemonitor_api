using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly bool _isDevelopment;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _isDevelopment = env.IsDevelopment();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // deixa Swagger rodar sem interceptar
            if (context.Request.Path.StartsWithSegments("/swagger"))
            {
                await _next(context);
                return;
            }

            await _next(context);
        }
        catch (Exception ex)
        {
            // log estruturado
            _logger.LogError(ex, "❌ Exceção capturada no pipeline de requisição.");

            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json";

                var errorResponse = new
                {
                    success = false,
                    message = _isDevelopment ? ex.Message : "Ocorreu um erro interno no servidor.",
                    error = _isDevelopment ? ex.InnerException?.Message : null,
                    // Em produção não expõe stack trace
                    stack = _isDevelopment ? ex.StackTrace : null,
                    timestamp = DateTime.UtcNow,
                    path = context.Request.Path
                };

                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, options));
            }
            else
            {
                _logger.LogWarning("⚠️ A resposta já foi iniciada, não foi possível modificar o corpo do erro.");
            }
        }
    }
}
