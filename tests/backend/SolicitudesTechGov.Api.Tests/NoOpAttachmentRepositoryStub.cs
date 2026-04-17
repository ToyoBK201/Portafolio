using SolicitudesTechGov.Application.Abstractions;
using SolicitudesTechGov.Application.Requests.Dtos;

namespace SolicitudesTechGov.Api.Tests;

/// <summary>
/// Sustituto cuando las pruebas eliminan EF/<see cref="SolicitudesTechGov.Infrastructure.Persistence.SolicitudesTechGovDbContext"/>.
/// </summary>
internal sealed class NoOpAttachmentRepositoryStub : IRequestAttachmentRepository
{
    public Task<int> CountActiveAsync(Guid requestId, CancellationToken cancellationToken)
    {
        _ = requestId;
        _ = cancellationToken;
        return Task.FromResult(0);
    }

    public Task<AttachmentFileInfo?> GetActiveByIdAsync(Guid requestId, Guid attachmentId, CancellationToken cancellationToken)
    {
        _ = requestId;
        _ = attachmentId;
        _ = cancellationToken;
        return Task.FromResult<AttachmentFileInfo?>(null);
    }

    public Task<IReadOnlyList<RequestAttachmentDto>> ListActiveAsync(Guid requestId, CancellationToken cancellationToken)
    {
        _ = requestId;
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<RequestAttachmentDto>>(Array.Empty<RequestAttachmentDto>());
    }

    public Task<(RequestAttachmentDto? Item, string? ErrorMessage)> TryAddAndSaveAsync(
        Guid requestId,
        Guid uploadedByUserId,
        string originalFileName,
        string contentType,
        Stream content,
        long contentLength,
        CancellationToken cancellationToken)
    {
        _ = requestId;
        _ = uploadedByUserId;
        _ = originalFileName;
        _ = contentType;
        _ = content;
        _ = contentLength;
        _ = cancellationToken;
        return Task.FromResult<(RequestAttachmentDto?, string?)>((null, "Adjuntos deshabilitados en pruebas."));
    }
}
