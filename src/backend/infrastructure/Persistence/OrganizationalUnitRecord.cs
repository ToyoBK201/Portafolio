namespace SolicitudesTechGov.Infrastructure.Persistence;

public sealed class OrganizationalUnitRecord
{
    public int UnitId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
