using SolicitudesTechGov.Application.Requests.Dtos;

namespace SolicitudesTechGov.Application.Abstractions;

public interface IRequestAttachmentRepository
{
    Task<IReadOnlyList<RequestAttachmentDto>> ListActiveAsync(Guid requestId, CancellationToken cancellationToken);

    Task<int> CountActiveAsync(Guid requestId, CancellationToken cancellationToken);

    /// <summary>
    /// Persiste metadatos y el stream en almacén; ejecuta SaveChanges dentro del flujo.
    /// </summary>
    Task<(RequestAttachmentDto? Item, string? ErrorMessage)> TryAddAndSaveAsync(
        Guid requestId,
        Guid uploadedByUserId,
        string originalFileName,
        string contentType,
        Stream content,
        long contentLength,
        CancellationToken cancellationToken);

    Task<AttachmentFileInfo?> GetActiveByIdAsync(Guid requestId, Guid attachmentId, CancellationToken cancellationToken);
}

/// <summary>
/// Metadatos + ruta relativa en almacén para descargar.
/// </summary>
public sealed record AttachmentFileInfo(
    string FileName,
    string ContentType,
    string RelativeStoragePath);
