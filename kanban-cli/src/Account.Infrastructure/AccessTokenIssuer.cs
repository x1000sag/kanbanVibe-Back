using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Account.Domain;
using Microsoft.IdentityModel.Tokens;

namespace Account.Infrastructure;

public sealed class AccessTokenIssuer
{
    public const string Issuer = "kanban-account";
    public const string Audience = "kanban-account";
    private const int LifetimeSeconds = 43200;

    private readonly SymmetricSecurityKey _signingKey;
    private readonly JwtSecurityTokenHandler _handler = new();

    public AccessTokenIssuer(string signingKeyUtf8)
    {
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKeyUtf8));
    }

    public string Issue(Guid accountId, Guid accessId, DateTimeOffset issuedAt)
    {
        var iat = AccessLifetime.TruncateToWholeSecondUtc(issuedAt);
        var iatSeconds = new DateTimeOffset(iat.UtcDateTime, TimeSpan.Zero).ToUnixTimeSeconds();

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, accountId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, accessId.ToString()),
            }),
            IssuedAt = DateTimeOffset.FromUnixTimeSeconds(iatSeconds).UtcDateTime,
            Expires = DateTimeOffset.FromUnixTimeSeconds(iatSeconds + LifetimeSeconds).UtcDateTime,
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256),
        };

        var token = _handler.CreateToken(descriptor);
        return _handler.WriteToken(token);
    }

    public static DateTimeOffset ExpiresAtFromIssued(DateTimeOffset issuedAt) => AccessLifetime.ExpiresAt(issuedAt);
}