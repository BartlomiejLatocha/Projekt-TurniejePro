using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using TurniejePro.Api.Auth;
using Xunit;

namespace TurniejePro.Tests;

public class JwtTokenServiceTests
{
    [Fact]
    public void CreateToken_ShouldGenerateValidTokenWithCorrectClaimsAndExpiration()
    {
        // Arrange
        var jwtOptions = new JwtOptions
        {
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            Key = "TestSecretKey_MustBeAtLeast32CharactersLong_ForJwtValidation!",
            ExpiresMinutes = 30
        };
        var optionsMock = Options.Create(jwtOptions);
        var tokenService = new JwtTokenService(optionsMock);

        var userId = Guid.NewGuid();
        var email = "test@domain.com";
        var roles = new[] { "Admin", "Kibic" };
        var beforeCreation = DateTimeOffset.UtcNow;

        // Act
        var result = tokenService.CreateToken(userId, email, roles);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(email, result.Email);
        Assert.Equal(roles, result.Roles);
        Assert.True(result.ExpiresAt >= beforeCreation.AddMinutes(29) && result.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(31));

        // Dekodowanie i weryfikacja zawartości tokena JWT
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(result.AccessToken);

        Assert.Equal(userId.ToString(), jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value);
        Assert.Equal(email, jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value);
        Assert.Equal(email, jwtToken.Claims.FirstOrDefault(c => c.Type == "name")?.Value);
        Assert.NotNull(jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti));

        var roleClaims = jwtToken.Claims.Where(c => c.Type == "role").Select(c => c.Value).ToList();
        Assert.Contains("Admin", roleClaims);
        Assert.Contains("Kibic", roleClaims);
    }
}
