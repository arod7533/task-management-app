using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TaskManager.Api.Auth;

public class JwtOptions
{
    public string Issuer { get; set; } = "TaskManager";
    public string Audience { get; set; } = "TaskManager";
    public string SigningKey { get; set; } = string.Empty;
    public int ExpiryHours { get; set; } = 24 * 7;
}

public interface IJwtIssuer
{
    string Issue(Guid userId, string email);
    DateTimeOffset Expiry { get; }
}

public class JwtIssuer : IJwtIssuer
{
    private readonly JwtOptions _opts;
    private readonly SigningCredentials _signing;

    public JwtIssuer(JwtOptions opts)
    {
        if (opts.SigningKey.Length < 32)
            throw new InvalidOperationException(
                "JWT signing key must be at least 32 bytes. Set Jwt:SigningKey in configuration.");
        _opts = opts;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.SigningKey));
        _signing = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public DateTimeOffset Expiry => DateTimeOffset.UtcNow.AddHours(_opts.ExpiryHours);

    public string Issue(Guid userId, string email)
    {
        var token = new JwtSecurityToken(
            issuer: _opts.Issuer,
            audience: _opts.Audience,
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            },
            expires: Expiry.UtcDateTime,
            signingCredentials: _signing);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
