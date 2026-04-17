namespace SolicitudesTechGov.Api.Admin;

public sealed record CreateOrganizationalUnitJson(string Code, string Name);

public sealed record PatchOrganizationalUnitJson(string? Code, string? Name, bool? IsActive);

public sealed record CreateAppUserJson(string Email, string DisplayName);

public sealed record RoleAssignmentJson(byte RoleId, int? OrganizationalUnitId);

public sealed record PutUserRolesJson(IReadOnlyList<RoleAssignmentJson> Assignments);
