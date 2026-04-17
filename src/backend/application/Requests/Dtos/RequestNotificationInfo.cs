namespace SolicitudesTechGov.Application.Requests.Dtos;

public sealed record RequestNotificationInfo(
    Guid RequestId,
    string Title,
    Guid RequesterUserId,
    Guid? AssignedAnalystUserId,
    Guid? AssignedImplementerUserId);
