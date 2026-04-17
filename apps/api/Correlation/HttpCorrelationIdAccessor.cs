using SolicitudesTechGov.Application.Abstractions;

namespace SolicitudesTechGov.Api.Correlation;

internal sealed class HttpCorrelationIdAccessor(IHttpContextAccessor httpContextAccessor) : ICorrelationIdAccessor
{
    public Guid? GetCorrelationId()
    {
        var http = httpContextAccessor.HttpContext;
        if (http?.Items.TryGetValue(CorrelationIdConstants.ItemsKey, out var value) == true && value is Guid id)
        {
            return id;
        }

        return null;
    }
}
