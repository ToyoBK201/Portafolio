using SolicitudesTechGov.Api.Correlation;

namespace SolicitudesTechGov.Api;

/// <summary>Cuerpo alineado a ProblemDetails (RFC 7807) + correlationId para soporte.</summary>
internal static class ApiProblemBody
{
    public static object For(HttpContext http, int status, string title, string? detail)
    {
        var correlationId = http.Items.TryGetValue(CorrelationIdConstants.ItemsKey, out var c) && c is Guid g
            ? g.ToString()
            : http.TraceIdentifier;

        var typeUri = status switch
        {
            400 => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            403 => "https://tools.ietf.org/html/rfc7231#section-6.5.3",
            404 => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            409 => "https://tools.ietf.org/html/rfc7231#section-6.5.8",
            429 => "https://tools.ietf.org/html/rfc6585#section-4",
            _ => "about:blank"
        };

        return new
        {
            type = typeUri,
            title,
            status,
            detail = string.IsNullOrWhiteSpace(detail) ? title : detail,
            correlationId
        };
    }
}
