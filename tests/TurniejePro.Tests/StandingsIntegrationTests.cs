using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TurniejePro.Contracts.Standings;
using Xunit;

namespace TurniejePro.Tests;

public class StandingsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestTokens _testTokens;

    public StandingsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _testTokens = new TestTokens(factory.Services);
    }

    [Fact]
    public async Task GET_Standings_UnknownTournament_Returns404NotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/tournaments/{Guid.NewGuid()}/standings");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GET_Standings_TournamentWithoutTeams_ReturnsEmptyTable()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", _testTokens.ForRoles("Organizer"));

        var createResponse = await _client.PostAsJsonAsync("/api/v1/tournaments", new
        {
            name = $"Tabela {Guid.NewGuid():N}",
            discipline = "Pilka nozna",
            format = "League",
            startsAt = DateTimeOffset.UtcNow.AddDays(10),
            maxTeams = 4
        });
        var tournament = await createResponse.Content.ReadFromJsonAsync<TournamentIdOnly>();

        // Act
        var standings = await _client.GetFromJsonAsync<StandingsResponse>($"/api/v1/tournaments/{tournament!.Id}/standings");

        // Assert
        Assert.NotNull(standings);
        Assert.Equal(tournament.Id, standings!.TournamentId);
        Assert.Empty(standings.Rows);
    }

    private sealed record TournamentIdOnly(Guid Id);
}
