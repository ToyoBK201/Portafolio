using SolicitudesTechGov.Application.Requests.Dtos;

namespace SolicitudesTechGov.Application.Abstractions;

public interface IRequestCommentRepository
{
    Task<IReadOnlyList<RequestCommentDto>> ListForRequestAsync(
        Guid requestId,
        bool includeInternal,
        CancellationToken cancellationToken);

    Task<(RequestCommentDto? Item, string? ErrorMessage)> TryAddAndSaveAsync(
        Guid requestId,
        Guid authorUserId,
        string body,
        bool isInternal,
        CancellationToken cancellationToken);
}
