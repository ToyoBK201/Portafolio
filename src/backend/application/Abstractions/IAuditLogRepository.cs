using SolicitudesTechGov.Application.Requests.Dtos;

namespace SolicitudesTechGov.Application.Abstractions;

public interface IAuditLogRepository
{
    Task<PagedResult<AdminAuditLogEntryDto>> ListGlobalAsync(
        int page,
        int pageSize,
        string? entityType,
        string? action,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RequestAuditEntryDto>> ListForRequestAsync(
        Guid requestId,
        CancellationToken cancellationToken);

    Task AddRequestCreatedAsync(
        Guid requestId,
        Guid actorUserId,
        byte toStatusId,
        CancellationToken cancellationToken);

    Task AddRequestTransitionAsync(
        Guid requestId,
        Guid actorUserId,
        string actorRole,
        string transition,
        byte fromStatusId,
        byte toStatusId,
        string? reason,
        CancellationToken cancellationToken);

    /// <summary>Eventos de administración (catálogos, usuarios) sin <see cref="RequestId"/> (docs/06 D1, D2).</summary>
    Task AddAdminCatalogEventAsync(
        Guid actorUserId,
        string actorRole,
        string action,
        string entityType,
        string entityId,
        string? payloadSummary,
        CancellationToken cancellationToken);
}
