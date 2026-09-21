using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ExpenseMcp.SecurityTests;

public static class TestTokens
{
    public static string Create(
        string audience = ExpenseMcpFactory.Audience,
        string clientId = "finance-admin",
        string scope = "expenses.read expenses.approve")
    {
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = ExpenseMcpFactory.Issuer,
            Audience = audience,
            NotBefore = now.AddMinutes(-1),
            Expires = now.AddMinutes(5),
            IssuedAt = now,
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", "user-42"),
                new Claim("client_id", clientId),
                new Claim("tenant_id", "tenant-a"),
                new Claim("scope", scope),
                new Claim("jti", Guid.NewGuid().ToString("N"))
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ExpenseMcpFactory.SigningKey)),
                SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
