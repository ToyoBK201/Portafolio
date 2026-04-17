namespace SolicitudesTechGov.Infrastructure.Persistence;

public sealed class AppUserRecord
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? ExternalSubjectId { get; set; }
    /// <summary>PBKDF2 (formato propio). Null si el usuario solo usa proveedor externo.</summary>
    public string? PasswordHash { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
