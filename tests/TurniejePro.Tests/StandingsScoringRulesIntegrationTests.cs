using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TurniejePro.Api.Auth;
using TurniejePro.Contracts.Matches;
using TurniejePro.Contracts.Standings;
using TurniejePro.Data;
using TurniejePro.Domain;
using TurniejePro.Domain.Enums;

namespace TurniejePro.Tests;

/// <summary>
/// Zasady punktacji turnieju w działającym API (KAN-300): tabela liczy punkty według <c>ScoringRules</c> turnieju,
/// a wpis wyniku odrzuca remis, gdy zasady go nie dopuszczają.
/// </summary>
public class StandingsScoringRulesIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly Guid _organizerId = Guid.NewGuid();

    public StandingsScoringRulesIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData(3, 1, 0, 3, 0)]
    [InlineData(2, 1, 0, 2, 0)]
    [InlineData(5, 2, 1, 5, 1)]
    public async Task GetStandings_CountsPointsAccordingToTheTournamentScoringRules(int win, int draw, int loss, int expectedWinner, int expectedLoser)
    {
        var (tournamentId, winnerId, loserId) = await SeedFinishedMatchAsync(new ScoringRules(win, draw, loss), homeScore: 2, awayScore: 1);

        var standings = await _client.GetFromJsonAsync<StandingsResponse>($"/api/v1/tournaments/{tournamentId}/standings");

        Assert.NotNull(standings);
        Assert.Equal(expectedWinner, standings.Rows.Single(row => row.TeamId == winnerId).Points);
        Assert.Equal(expectedLoser, standings.Rows.Single(row => row.TeamId == loserId).Points);
    }

    [Fact]
    public async Task GetStandings_TournamentCreatedWithDefaultRules_UsesThreeOneZero()
    {
        var (tournamentId, winnerId, _) = await SeedFinishedMatchAsync(scoringRules: null, homeScore: 1, awayScore: 0);

        var standings = await _client.GetFromJsonAsync<StandingsResponse>($"/api/v1/tournaments/{tournamentId}/standings");

        Assert.Equal(3, standings!.Rows.Single(row => row.TeamId == winnerId).Points);
    }

    [Fact]
    public async Task SetResult_DrawInTournamentWithoutDraws_Returns400AndSavesNothing()
    {
        var (tournamentId, matchId) = await SeedScheduledMatchAsync(new ScoringRules(3, 1, 0, allowDraws: false));

        var response = await PutResultAsync(tournamentId, matchId, 1, 1);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(LoadMatch(matchId).HomeScore);
    }

    [Fact]
    public async Task SetResult_DrawInTournamentWithDraws_Returns200()
    {
        var (tournamentId, matchId) = await SeedScheduledMatchAsync(new ScoringRules(3, 1, 0, allowDraws: true));

        var response = await PutResultAsync(tournamentId, matchId, 1, 1);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<HttpResponseMessage> PutResultAsync(Guid tournamentId, Guid matchId, int homeScore, int awayScore)
    {
        using var scope = _factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<ITokenService>()
            .CreateToken(_organizerId, "organizer@example.com", [Roles.User]).AccessToken;

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/tournaments/{tournamentId}/matches/{matchId}/result")
        {
            Content = JsonContent.Create(new UpdateMatchResultRequest(homeScore, awayScore))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await _client.SendAsync(request);
    }

    private async Task<(Guid TournamentId, Guid WinnerId, Guid LoserId)> SeedFinishedMatchAsync(ScoringRules? scoringRules, int homeScore, int awayScore)
    {
        var (tournamentId, matchId) = await SeedScheduledMatchAsync(scoringRules);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();
        var match = db.Matches.Single(m => m.Id == matchId);
        match.SetResult(homeScore, awayScore);
        await db.SaveChangesAsync();

        return (tournamentId, match.HomeTeamId!.Value, match.AwayTeamId!.Value);
    }

    private async Task<(Guid TournamentId, Guid MatchId)> SeedScheduledMatchAsync(ScoringRules? scoringRules)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();

        var tournament = new Tournament(
            $"Turniej {Guid.NewGuid():N}",
            "Piłka nożna",
            TournamentFormat.League,
            DateTimeOffset.UtcNow.AddDays(7),
            8,
            _organizerId,
            scoringRules);
        var home = new Team("Gospodarze", tournament.Id);
        var away = new Team("Goście", tournament.Id);
        home.Accept();
        away.Accept();
        tournament.Teams.Add(home);
        tournament.Teams.Add(away);
        db.Tournaments.Add(tournament);

        var match = new Match(tournament.Id, 1, 1, home.Id, away.Id);
        db.Matches.Add(match);
        await db.SaveChangesAsync();

        return (tournament.Id, match.Id);
    }

    private Match LoadMatch(Guid matchId)
    {
        using var scope = _factory.Services.CreateScope();

        return scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>().Matches.Single(m => m.Id == matchId);
    }
}
