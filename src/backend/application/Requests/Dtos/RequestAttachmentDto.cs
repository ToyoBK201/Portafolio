namespace SolicitudesTechGov.Application.Requests.Dtos;

public sealed record RequestAttachmentDto(
    Guid AttachmentId,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime UploadedAtUtc);
