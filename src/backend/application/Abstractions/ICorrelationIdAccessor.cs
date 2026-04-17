namespace SolicitudesTechGov.Application.Abstractions;

/// <summary>
/// Correlación de solicitud HTTP (middleware) para auditoría y respuestas de error.
/// </summary>
public interface ICorrelationIdAccessor
{
    Guid? GetCorrelationId();
}
