using SolicitudesTechGov.Domain;

namespace SolicitudesTechGov.Domain.Entities;

public sealed class Request
{
    public Guid RequestId { get; init; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string BusinessJustification { get; private set; } = string.Empty;
    public RequestType RequestType { get; private set; }
    public Priority Priority { get; private set; }
    public int RequestingUnitId { get; private set; }
    public Guid RequesterUserId { get; private set; }
    public RequestStatus Status { get; private set; }
    public DateOnly? DesiredDate { get; private set; }
    public string? SpecificPayloadJson { get; private set; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Campos comunes de borrador validados y normalizados (crear y actualizar).</summary>
    public static (string Title, string Description, string BusinessJustification, RequestType RequestType, Priority Priority, int RequestingUnitId)
        ValidateDraftContent(
            string title,
            string description,
            string businessJustification,
            byte requestType,
            byte priority,
            int requestingUnitId)
    {
        if (!Enum.IsDefined(typeof(RequestType), requestType))
        {
            throw new DomainValidationException("Invalid request type.");
        }

        if (!Enum.IsDefined(typeof(Priority), priority))
        {
            throw new DomainValidationException("Invalid priority.");
        }

        var normalizedTitle = title.Trim();
        var normalizedDescription = description.Trim();
        var normalizedJustification = businessJustification.Trim();

        if (normalizedTitle.Length is < 5 or > 200)
        {
            throw new DomainValidationException("Title must have between 5 and 200 characters.");
        }

        if (normalizedDescription.Length is < 20 or > 8000)
        {
            throw new DomainValidationException("Description must have between 20 and 8000 characters.");
        }

        if (normalizedJustification.Length < 20)
        {
            throw new DomainValidationException("Business justification must have at least 20 characters.");
        }

        if (requestingUnitId <= 0)
        {
            throw new DomainValidationException("Requesting unit id is required.");
        }

        return (
            normalizedTitle,
            normalizedDescription,
            normalizedJustification,
            (RequestType)requestType,
            (Priority)priority,
            requestingUnitId);
    }

    public static Request CreateDraft(
        string title,
        string description,
        string businessJustification,
        RequestType requestType,
        Priority priority,
        int requestingUnitId,
        Guid requesterUserId,
        DateOnly? desiredDate,
        string? specificPayloadJson)
    {
        var (nt, nd, nj, rt, pr, unit) = ValidateDraftContent(
            title,
            description,
            businessJustification,
            (byte)requestType,
            (byte)priority,
            requestingUnitId);

        if (requesterUserId == Guid.Empty)
        {
            throw new DomainValidationException("Requester user id is required.");
        }

        return new Request
        {
            RequestId = Guid.NewGuid(),
            Title = nt,
            Description = nd,
            BusinessJustification = nj,
            RequestType = rt,
            Priority = pr,
            RequestingUnitId = unit,
            RequesterUserId = requesterUserId,
            DesiredDate = desiredDate,
            SpecificPayloadJson = string.IsNullOrWhiteSpace(specificPayloadJson) ? null : specificPayloadJson,
            Status = RequestStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Asigna campos ya validados con <see cref="ValidateDraftContent"/>; solo en <see cref="RequestStatus.Draft"/>.
    /// </summary>
    public void ApplyValidatedDraftUpdate(
        string normalizedTitle,
        string normalizedDescription,
        string normalizedJustification,
        RequestType requestType,
        Priority priority,
        int requestingUnitId,
        DateOnly? desiredDate,
        string? specificPayloadJson,
        DateTime updatedAtUtc)
    {
        if (Status != RequestStatus.Draft)
        {
            throw new DomainValidationException("Only draft requests can be updated.");
        }

        Title = normalizedTitle;
        Description = normalizedDescription;
        BusinessJustification = normalizedJustification;
        RequestType = requestType;
        Priority = priority;
        RequestingUnitId = requestingUnitId;
        DesiredDate = desiredDate;
        SpecificPayloadJson = string.IsNullOrWhiteSpace(specificPayloadJson) ? null : specificPayloadJson;
        UpdatedAtUtc = updatedAtUtc;
    }

    /// <summary>
    /// Persistencia / repositorio en memoria: el flujo de transiciones valida reglas en la capa de aplicación.
    /// </summary>
    public void ApplyStatusChange(RequestStatus nextStatus, DateTime updatedAtUtc)
    {
        Status = nextStatus;
        UpdatedAtUtc = updatedAtUtc;
    }
}
