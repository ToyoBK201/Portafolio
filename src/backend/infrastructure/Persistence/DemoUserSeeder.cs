using Microsoft.EntityFrameworkCore;
using SolicitudesTechGov.Infrastructure.Security;

namespace SolicitudesTechGov.Infrastructure.Persistence;

/// <summary>
/// En desarrollo, asegura un usuario con contraseña local para probar A1 sin depender del dev-token.
/// Credenciales: demo@mvp.local / Demo123!
/// </summary>
public static class DemoUserSeeder
{
    private static readonly Guid DemoUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static async Task EnsureAsync(SolicitudesTechGovDbContext db, CancellationToken cancellationToken = default)
    {
        const string email = "demo@mvp.local";
        const string displayName = "Usuario demo (login local)";
        var hash = Pbkdf2PasswordHasher.HashPassword("Demo123!");

        var user = await db.AppUsers.FirstOrDefaultAsync(x => x.UserId == DemoUserId, cancellationToken);
        if (user is null)
        {
            await db.AppUsers.AddAsync(
                new AppUserRecord
                {
                    UserId = DemoUserId,
                    Email = email,
                    DisplayName = displayName,
                    ExternalSubjectId = null,
                    PasswordHash = hash,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                },
                cancellationToken);
        }
        else
        {
            user.Email = email;
            user.DisplayName = displayName;
            user.IsActive = true;
            user.PasswordHash = hash;
        }

        var hasRequester = await db.UserRoles.AnyAsync(
            x => x.UserId == DemoUserId && x.RoleId == 1 && x.OrganizationalUnitId == null,
            cancellationToken);
        if (!hasRequester)
        {
            await db.UserRoles.AddAsync(
                new UserRoleRecord
                {
                    UserId = DemoUserId,
                    RoleId = 1,
                    OrganizationalUnitId = null,
                    AssignedAtUtc = DateTime.UtcNow
                },
                cancellationToken);
        }

        // Segundo rol para probar cambio de rol activo (docs/06 A2) sin datos manuales.
        var hasAnalyst = await db.UserRoles.AnyAsync(
            x => x.UserId == DemoUserId && x.RoleId == 3 && x.OrganizationalUnitId == null,
            cancellationToken);
        if (!hasAnalyst)
        {
            await db.UserRoles.AddAsync(
                new UserRoleRecord
                {
                    UserId = DemoUserId,
                    RoleId = 3,
                    OrganizationalUnitId = null,
                    AssignedAtUtc = DateTime.UtcNow
                },
                cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
