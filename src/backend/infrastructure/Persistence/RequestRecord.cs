namespace SolicitudesTechGov.Infrastructure.Persistence;

public sealed class RequestRecord
{
    public Guid RequestId { get; set; }
    public string? Folio { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string BusinessJustification { get; set; } = string.Empty;
    public byte RequestTypeId { get; set; }
    public byte PriorityId { get; set; }
    public int RequestingUnitId { get; set; }
    public Guid RequesterUserId { get; set; }
    public byte StatusId { get; set; }
    public DateOnly? DesiredDate { get; set; }
    public string? SpecificPayloadJson { get; set; }
    public Guid? AssignedAnalystUserId { get; set; }
    public Guid? AssignedImplementerUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
}
