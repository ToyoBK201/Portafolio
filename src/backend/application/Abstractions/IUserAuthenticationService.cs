namespace SolicitudesTechGov.Application.Abstractions;

public enum LoginFailureReason
{
    InvalidCredentials,
    AccountInactive,
    PasswordNotConfigured,
    NoRolesAssigned
}

/// <summary>Resultado de autenticación por email/contraseña (docs/06 A1).</summary>
public abstract record LoginOutcome;

public sealed record LoginSucceeded(Guid UserId, string RoleCode) : LoginOutcome;

public sealed record LoginFailed(LoginFailureReason Reason) : LoginOutcome;

public interface IUserAuthenticationService
{
    Task<LoginOutcome> TryLoginAsync(string email, string password, CancellationToken cancellationToken);
}
