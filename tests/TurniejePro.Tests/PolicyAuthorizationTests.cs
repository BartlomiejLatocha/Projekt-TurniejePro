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
        var response = await _client.GetAsync("/api/test-auth/create-tournament");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateTournamentPolicy_WithUserRole_Returns200Ok()
    {
        // Arrange
        var token = _tokenService.CreateToken(Guid.NewGuid(), "user@example.com", new[] { Roles.User }).AccessToken;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/test-auth/create-tournament");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RegisterTeamPolicy_WithUserRole_Returns200Ok()
    {
        // Arrange
        var token = _tokenService.CreateToken(Guid.NewGuid(), "user@example.com", new[] { Roles.User }).AccessToken;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/test-auth/register-team");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
