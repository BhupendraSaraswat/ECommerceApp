using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace ECommerce.Shared.Middleware
{
    // ✅ Ye middleware har request ka time aur details log karta hai
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(
            RequestDelegate next,
            ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                await _next(context);
            }
            finally
            {
                var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

                // ✅ Slow requests ko warn karo (500ms se zyada)
                if (elapsedMs > 500)
                {
                    _logger.LogWarning(
                        "SLOW REQUEST | {Method} {Path} | Status: {Status} | Time: {ElapsedMs}ms | User: {UserId}",
                        context.Request.Method,
                        context.Request.Path,
                        context.Response.StatusCode,
                        Math.Round(elapsedMs),
                        context.User?.FindFirst("userId")?.Value ?? "anonymous");
                }
                else
                {
                    _logger.LogInformation(
                        "{Method} {Path} | Status: {Status} | Time: {ElapsedMs}ms | User: {UserId}",
                        context.Request.Method,
                        context.Request.Path,
                        context.Response.StatusCode,
                        Math.Round(elapsedMs),
                        context.User?.FindFirst("userId")?.Value ?? "anonymous");
                }
            }
        }
    }
}