using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SolicitudesTechGov.Api.Auth;

public static class JwtAuthMethodClaims
{
    public const string ClaimType = "auth_method";
    public const string Password = "password";
    public const string Dev = "dev";
}

public sealed class JwtTokenIssuer(IConfiguration configuration)
{
    public string IssueToken(Guid userId, string role, TimeSpan lifetime, string authMethod = JwtAuthMethodClaims.Password)
    {
        var jwtSection = configuration.GetSection("Jwt");
        var key = jwtSection["SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is required.");
        var issuer = jwtSection["Issuer"] ?? "SolicitudesTechGov";
        var audience = jwtSection["Audience"] ?? "SolicitudesTechGov";

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.Role, role),
            new(JwtAuthMethodClaims.ClaimType, authMethod)
        };

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
