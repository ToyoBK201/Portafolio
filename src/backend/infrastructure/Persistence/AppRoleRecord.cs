namespace SolicitudesTechGov.Infrastructure.Persistence;

public sealed class AppRoleRecord
{
    public byte RoleId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string LabelEs { get; set; } = string.Empty;
    public byte SortOrder { get; set; }
}
