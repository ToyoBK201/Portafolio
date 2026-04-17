using Microsoft.EntityFrameworkCore;
using SolicitudesTechGov.Application.Abstractions;

namespace SolicitudesTechGov.Infrastructure.Persistence;

public sealed class SqlUserAuthenticationService : IUserAuthenticationService
{
    private readonly SolicitudesTechGovDbContext _db;

    public SqlUserAuthenticationService(SolicitudesTechGovDbContext db)
    {
        _db = db;
    }

    public async Task<LoginOutcome> TryLoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var normalized = email.Trim();
        if (normalized.Length < 3 || password.Length < 1)
        {
            return new LoginFailed(LoginFailureReason.InvalidCredentials);
        }

        var user = await _db.AppUsers.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Email.ToLower() == normalized.ToLower(),
                cancellationToken);

        if (user is null)
        {
            return new LoginFailed(LoginFailureReason.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            return new LoginFailed(LoginFailureReason.AccountInactive);
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return new LoginFailed(LoginFailureReason.PasswordNotConfigured);
        }

        if (!Security.Pbkdf2PasswordHasher.Verify(password, user.PasswordHash))
        {
            return new LoginFailed(LoginFailureReason.InvalidCredentials);
        }

        var roleCode = await (
                from ur in _db.UserRoles.AsNoTracking()
                join ar in _db.AppRoles.AsNoTracking() on ur.RoleId equals ar.RoleId
                where ur.UserId == user.UserId
                orderby ar.SortOrder
                select ar.Code)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrEmpty(roleCode))
        {
            return new LoginFailed(LoginFailureReason.NoRolesAssigned);
        }

        return new LoginSucceeded(user.UserId, roleCode);
    }
}
