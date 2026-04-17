using Microsoft.Extensions.Logging;

namespace SolicitudesTechGov.Api.Correlation;

internal sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        Guid correlationId;
        if (context.Request.Headers.TryGetValue(CorrelationIdConstants.HeaderName, out var headerValue) &&
            Guid.TryParse(headerValue.ToString(), out var parsed))
        {
            correlationId = parsed;
        }
        else
        {
            correlationId = Guid.NewGuid();
        }

        context.Items[CorrelationIdConstants.ItemsKey] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdConstants.HeaderName] = correlationId.ToString();
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await _next(context);
        }
    }
}
