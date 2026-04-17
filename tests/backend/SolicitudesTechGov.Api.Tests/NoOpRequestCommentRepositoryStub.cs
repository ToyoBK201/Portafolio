using SolicitudesTechGov.Application.Abstractions;
using SolicitudesTechGov.Application.Requests.Dtos;

namespace SolicitudesTechGov.Api.Tests;

internal sealed class NoOpRequestCommentRepositoryStub : IRequestCommentRepository
{
    public Task<IReadOnlyList<RequestCommentDto>> ListForRequestAsync(
        Guid requestId,
        bool includeInternal,
        CancellationToken cancellationToken)
    {
        _ = requestId;
        _ = includeInternal;
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<RequestCommentDto>>(Array.Empty<RequestCommentDto>());
    }

    public Task<(RequestCommentDto? Item, string? ErrorMessage)> TryAddAndSaveAsync(
        Guid requestId,
        Guid authorUserId,
        string body,
        bool isInternal,
        CancellationToken cancellationToken)
    {
        _ = requestId;
        _ = authorUserId;
        _ = body;
        _ = isInternal;
        _ = cancellationToken;
        return Task.FromResult<(RequestCommentDto?, string?)>((null, "Comentarios deshabilitados en pruebas."));
    }
}
