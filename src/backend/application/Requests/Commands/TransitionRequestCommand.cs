namespace SolicitudesTechGov.Application.Requests.Commands;

public sealed record TransitionRequestCommand(
    Guid RequestId,
    string Transition,
    string? Reason,
    Guid ActorUserId,
    string ActorRole,
    bool IsSystemAdministrator);
