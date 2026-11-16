using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TurniejePro.Contracts.Matches;
using TurniejePro.Contracts.Standings;
using TurniejePro.Contracts.Teams;
using Xunit;

namespace TurniejePro.Tests;

public class GroupStageIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly int[] ExpectedGroups = [1, 2];

    private readonly HttpClient _client;
    private readonly TestTokens _testTokens;

    public GroupStageIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _testTokens = new TestTokens(factory.Services);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _testTokens.ForRoles("Organizer"));
    }

    [Fact]
    public async Task GroupsAndCup_GeneratesGroupMatchesAndTablePerGroup()
    {
        var tournamentId = await CreateTournamentAsync();
        for (var i = 1; i <= 6; i++)
        {
            await RegisterAndAcceptAsync(tournamentId, $"Grupowa {i}");
        }

        var generate = await _client.PostAsync($"/api/v1/tournaments/{tournamentId}/schedule", null);
        var schedule = await _client.GetFromJsonAsync<ScheduleResponse>($"/api/v1/tournaments/{tournamentId}/schedule");
        var standings = await _client.GetFromJsonAsync<StandingsResponse>($"/api/v1/tournaments/{tournamentId}/standings");

        Assert.Equal(HttpStatusCode.Created, generate.StatusCode);
        var groupMatches = schedule!.Rounds.SelectMany(r => r.Matches).Where(m => m.Group is not null).ToList();
        Assert.Equal(6, groupMatches.Count);
        Assert.Equal(ExpectedGroups, groupMatches.Select(m => m.Group!.Value).Distinct().Order().ToArray());
        Assert.Equal(ExpectedGroups, standings!.Rows.Select(r => r.Group!.Value).Distinct().Order().ToArray());
    }

    [Fact]
    public async Task GroupsAndCup_TooFewTeams_Returns400()
    {
        var tournamentId = await CreateTournamentAsync();
        for (var i = 1; i <= 5; i++)
        {
            await RegisterAndAcceptAsync(tournamentId, $"Za mala {i}");
        }

        var generate = await _client.PostAsync($"/api/v1/tournaments/{tournamentId}/schedule", null);

        Assert.Equal(HttpStatusCode.BadRequest, generate.StatusCode);
    }

    private async Task<Guid> CreateTournamentAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/tournaments", new
        {
            name = $"Grupy {Guid.NewGuid():N}",
            discipline = "football",
            format = "GroupsAndCup",
            startsAt = DateTimeOffset.UtcNow.AddDays(10),
            maxTeams = 16
        });
        var created = await response.Content.ReadFromJsonAsync<TournamentIdOnly>();
        return created!.Id;
    }

    private async Task RegisterAndAcceptAsync(Guid tournamentId, string name)
    {
        var team = await _client.PostAsJsonAsync("/api/v1/teams", new
        {
            tournamentId,
            name = $"{name} {Guid.NewGuid():N}",
            contactEmail = (string?)null,
            players = Array.Empty<object>()
        });
        var created = await team.Content.ReadFromJsonAsync<TeamResponse>();
        await _client.PatchAsync($"/api/v1/teams/{created!.Id}/accept", null);
    }

    private sealed record TournamentIdOnly(Guid Id);
}
