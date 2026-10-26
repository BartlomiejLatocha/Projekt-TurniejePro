using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using TurniejePro.Api.Auth;
using Xunit;

namespace TurniejePro.Tests;

public class PolicyAuthorizationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ITokenService _tokenService;

    public PolicyAuthorizationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();

        var scope = factory.Services.CreateScope();
        _tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
    }

    [Fact]
    public async Task Endpoint_WithoutToken_Returns401Unauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/test-auth/organizer-only");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Endpoint_WithInsufficientRole_Returns403Forbidden()
    {
        // Arrange: Kibic próbuje wywołać endpoint wymagający polityki ManageTournaments (Organizator)
        var token = _tokenService.CreateToken(Guid.NewGuid(), "kibic@example.com", new[] { Roles.Fan }).AccessToken;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/test-auth/organizer-only");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Endpoint_WithRequiredRole_Returns200Ok()
    {
        // Arrange: Organizator wywołuje endpoint chroniony polityką ManageTournaments
        var token = _tokenService.CreateToken(Guid.NewGuid(), "organizator@example.com", new[] { Roles.Organizer }).AccessToken;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/test-auth/organizer-only");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
