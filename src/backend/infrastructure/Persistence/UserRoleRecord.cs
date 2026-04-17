namespace SolicitudesTechGov.Infrastructure.Persistence;

public sealed class UserRoleRecord
{
    public long UserRoleId { get; set; }
    public Guid UserId { get; set; }
    public byte RoleId { get; set; }
    public int? OrganizationalUnitId { get; set; }
    public DateTime AssignedAtUtc { get; set; }
}
