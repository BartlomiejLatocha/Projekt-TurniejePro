using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TurniejePro.Api.Auth;

namespace TurniejePro.Tests;

/// <summary>
/// Wspólny pomocnik testów autoryzacji: wystawia tokeny JWT bez logowania przez HTTP.
/// Obok poprawnych tokenów potrafi złożyć tokeny wadliwe (wygasły, zły klucz, zły issuer/audience),
/// których zwykły <see cref="ITokenService"/> nie utworzy.
/// </summary>
public sealed class TestTokens
{
    private readonly IServiceProvider _services;
    private readonly JwtOptions _jwt;

    public TestTokens(IServiceProvider services)
    {
        _services = services;
        _jwt = services.GetRequiredService<IOptions<JwtOptions>>().Value;
    }

    /// <summary>Poprawny token wydany przez aplikację, z podanymi rolami (bez ról, gdy nic nie podano).</summary>
    public string ForRoles(params string[] roles)
    {
        using var scope = _services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        return tokenService.CreateToken(Guid.NewGuid(), "test@example.com", roles).AccessToken;
    }

    /// <summary>Poprawny token, który wygasł godzinę temu (poza tolerancją zegara).</summary>
    public string Expired(params string[] roles)
    {
        var now = DateTime.UtcNow;
        return Build(roles, _jwt.Key, _jwt.Issuer, _jwt.Audience, now.AddHours(-2), now.AddHours(-1));
    }

    /// <summary>Token z poprawną treścią, ale podpisany innym kluczem niż zna API.</summary>
    public string SignedWithWrongKey(params string[] roles) =>
        Build(roles, "AnotherSecretKey_NotUsedByTheApi_AtLeast32CharactersLong!", _jwt.Issuer, _jwt.Audience, ValidFrom(), ValidUntil());

    /// <summary>Token podpisany właściwym kluczem, ale wydany przez kogoś innego (zły issuer).</summary>
    public string WithWrongIssuer(params string[] roles) =>
        Build(roles, _jwt.Key, "ObcyIssuer", _jwt.Audience, ValidFrom(), ValidUntil());

    /// <summary>Token podpisany właściwym kluczem, ale przeznaczony dla innego odbiorcy (zły audience).</summary>
    public string WithWrongAudience(params string[] roles) =>
        Build(roles, _jwt.Key, _jwt.Issuer, "ObcyOdbiorca", ValidFrom(), ValidUntil());

    private static DateTime ValidFrom() => DateTime.UtcNow;

    private static DateTime ValidUntil() => DateTime.UtcNow.AddHours(1);

    private static string Build(
        IEnumerable<string> roles,
        string key,
        string issuer,
        string audience,
        DateTime notBefore,
        DateTime expires)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, "test@example.com"),
            new("name", "test@example.com"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = notBefore,
            Expires = expires,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
