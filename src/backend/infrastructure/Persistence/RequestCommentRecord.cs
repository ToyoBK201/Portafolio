namespace SolicitudesTechGov.Infrastructure.Persistence;

public sealed class RequestCommentRecord
{
    public Guid CommentId { get; set; }
    public Guid RequestId { get; set; }
    public Guid AuthorUserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
