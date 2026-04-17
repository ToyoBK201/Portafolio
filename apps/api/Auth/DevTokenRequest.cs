namespace SolicitudesTechGov.Api.Auth;

public sealed record DevTokenRequest(Guid UserId, string Role);
