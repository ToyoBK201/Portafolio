namespace SolicitudesTechGov.Application.Requests.Dtos;

public sealed record RequestCommentDto(
    Guid CommentId,
    Guid AuthorUserId,
    string AuthorDisplayName,
    string Body,
    bool IsInternal,
    DateTime CreatedAtUtc);
