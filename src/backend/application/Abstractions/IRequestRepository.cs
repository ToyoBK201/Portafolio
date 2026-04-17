using SolicitudesTechGov.Domain.Entities;
using SolicitudesTechGov.Application.Requests.Dtos;

namespace SolicitudesTechGov.Application.Abstractions;

public interface IRequestRepository
{
    Task AddAsync(Request request, CancellationToken cancellationToken);
    Task<RequestDto?> GetByIdAsync(Guid requestId, CancellationToken cancellationToken);

    Task<RequestNotificationInfo?> GetNotificationInfoAsync(Guid requestId, CancellationToken cancellationToken);
    Task<bool> TryUpdateStatusAsync(
        Guid requestId,
        string expectedCurrentStatus,
        string nextStatus,
        DateTime updatedAtUtc,
        DateTime? submittedAtUtc,
        CancellationToken cancellationToken);

    /// <summary>Persiste cambios de borrador (campos ya validados en dominio).</summary>
    Task<bool> TryUpdateDraftAsync(
        Guid requestId,
        string title,
        string description,
        string businessJustification,
        byte requestTypeId,
        byte priorityId,
        int requestingUnitId,
        DateOnly? desiredDate,
        string? specificPayloadJson,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken);
    Task<PagedResult<RequestDto>> ListAsync(
        string? status,
        Guid? requesterUserId,
        DateTime? createdFromUtc,
        DateTime? createdToUtc,
        int page,
        int pageSize,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken);

    /// <summary>
    /// Conteos por estado. Si <paramref name="requesterUserIdScopeOnly"/> tiene valor, solo solicitudes de ese solicitante.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(
        Guid? requesterUserIdScopeOnly,
        CancellationToken cancellationToken);
}
