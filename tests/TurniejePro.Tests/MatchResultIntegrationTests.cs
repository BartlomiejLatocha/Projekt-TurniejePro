using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TurniejePro.Contracts.Matches;
using TurniejePro.Data;
using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using Xunit;

namespace TurniejePro.Tests;

public class MatchResultIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly TestTokens _testTokens;

    public MatchResultIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _testTokens = new TestTokens(factory.Services);
    }

    [Fact]
    public async Task PUT_SetResult_LeagueMatch_Returns200OKAndUpdatesScore()
    {
        // Arrange
        var token = _testTokens.ForRoles("Admin", "Organizer");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (tournamentId, matchId) = await SeedLeagueTournamentWithMatchAsync();
        var request = new UpdateMatchResultRequest(2, 1);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/tournaments/{tournamentId}/matches/{matchId}/result", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<MatchResponse>();
        Assert.NotNull(result);
        Assert.Equal(2, result!.HomeScore);
        Assert.Equal(1, result.AwayScore);
        Assert.Equal("Finished", result.Status.ToString());
    }

    [Fact]
    public async Task PUT_SetResult_CupDraw_Returns400BadRequest()
    {
        // Arrange
        var token = _testTokens.ForRoles("Admin", "Organizer");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (tournamentId, matchId) = await SeedCupTournamentWithMatchAsync();
        var request = new UpdateMatchResultRequest(2, 2);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/tournaments/{tournamentId}/matches/{matchId}/result", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PUT_SetResult_NonExistentMatch_Returns404NotFound()
    {
        // Arrange
        var token = _testTokens.ForRoles("Admin", "Organizer");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (tournamentId, _) = await SeedLeagueTournamentWithMatchAsync();
        var request = new UpdateMatchResultRequest(1, 0);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/tournaments/{tournamentId}/matches/{Guid.NewGuid()}/result", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PUT_SetResult_ByeMatch_Returns409Conflict()
    {
        // Arrange
        var token = _testTokens.ForRoles("Admin", "Organizer");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (tournamentId, byeMatchId) = await SeedByeMatchAsync();
        var request = new UpdateMatchResultRequest(1, 0);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/tournaments/{tournamentId}/matches/{byeMatchId}/result", request);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PUT_SetResult_Authorization_FanReturns403_NoTokenReturns401()
    {
        // Arrange
        var (tournamentId, matchId) = await SeedLeagueTournamentWithMatchAsync();
        var request = new UpdateMatchResultRequest(1, 0);

        // 1. Brak tokenu -> 401 Unauthorized
        _client.DefaultRequestHeaders.Authorization = null;
        var unauthResponse = await _client.PutAsJsonAsync($"/api/v1/tournaments/{tournamentId}/matches/{matchId}/result", request);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthResponse.StatusCode);

        // 2. Token Kibica -> 403 Forbidden
        var fanToken = _testTokens.ForRoles("Fan");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fanToken);
        var forbiddenResponse = await _client.PutAsJsonAsync($"/api/v1/tournaments/{tournamentId}/matches/{matchId}/result", request);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
    }

    private async Task<(Guid TournamentId, Guid MatchId)> SeedLeagueTournamentWithMatchAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();

        var tournament = new Tournament($"Liga {Guid.NewGuid()}", "Opis", TournamentFormat.League, DateTimeOffset.UtcNow.AddDays(1), 8, null);
        var t1 = new Team("Drużyna A", tournament.Id); t1.Accept();
        var t2 = new Team("Drużyna B", tournament.Id); t2.Accept();
        var match = new Match(tournament.Id, 1, 1, t1.Id, t2.Id);

        db.Tournaments.Add(tournament);
        db.Teams.AddRange(t1, t2);
        db.Matches.Add(match);
        await db.SaveChangesAsync();

        return (tournament.Id, match.Id);
    }

    private async Task<(Guid TournamentId, Guid MatchId)> SeedCupTournamentWithMatchAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();

        var tournament = new Tournament($"Puchar {Guid.NewGuid()}", "Opis", TournamentFormat.Cup, DateTimeOffset.UtcNow.AddDays(1), 4, null);
        var t1 = new Team("Drużyna A", tournament.Id); t1.Accept();
        var t2 = new Team("Drużyna B", tournament.Id); t2.Accept();
        var match = new Match(tournament.Id, 1, 1, t1.Id, t2.Id);

        db.Tournaments.Add(tournament);
        db.Teams.AddRange(t1, t2);
        db.Matches.Add(match);
        await db.SaveChangesAsync();

        return (tournament.Id, match.Id);
    }

    private async Task<(Guid TournamentId, Guid ByeMatchId)> SeedByeMatchAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();

        var tournament = new Tournament($"Puchar Bye {Guid.NewGuid()}", "Opis", TournamentFormat.Cup, DateTimeOffset.UtcNow.AddDays(1), 5, null);
        var t1 = new Team("Drużyna A", tournament.Id); t1.Accept();
        var byeMatch = new Match(tournament.Id, 1, 1, t1.Id, awayTeamId: null);
        byeMatch.MarkAsBye();

        db.Tournaments.Add(tournament);
        db.Teams.Add(t1);
        db.Matches.Add(byeMatch);
        await db.SaveChangesAsync();

        return (tournament.Id, byeMatch.Id);
    }
}
