using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using TurniejePro.Api.Auth;
using TurniejePro.Contracts.Teams;
using TurniejePro.Data;
using TurniejePro.Domain;
using TurniejePro.Domain.Enums;

namespace TurniejePro.Tests;

/// <summary>
/// Sprawdza w działającym API, że zgłoszenie drużyny jest blokowane po rozpoczęciu turnieju, po upływie jego terminu
/// i po osiągnięciu limitu drużyn (KAN-76). Turnieje z terminem w przeszłości trafiają do bazy bezpośrednio,
/// bo API nie pozwala ich utworzyć.
/// </summary>
public class TeamRegistrationBlockTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TeamRegistrationBlockTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", new TestTokens(factory.Services).ForRoles(Roles.Captain));
    }

    [Fact]
    public async Task RegisterTeam_BeforeDeadlineAndBelowLimit_Returns201()
    {
        var tournamentId = await SeedTournamentAsync(startsAt: DateTimeOffset.UtcNow.AddDays(7), maxTeams: 2);

        var response = await RegisterTeamAsync(tournamentId, "Orły");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task RegisterTeam_WhenLimitReached_Returns400AndDoesNotSaveTeam()
    {
        var tournamentId = await SeedTournamentAsync(startsAt: DateTimeOffset.UtcNow.AddDays(7), maxTeams: 2);
        Assert.Equal(HttpStatusCode.Created, (await RegisterTeamAsync(tournamentId, "Orły")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await RegisterTeamAsync(tournamentId, "Sokoły")).StatusCode);

        var response = await RegisterTeamAsync(tournamentId, "Jastrzębie");

        await AssertRegistrationBlockedAsync(response, expectedDetailFragment: "limit");
        Assert.Equal(2, await CountTeamsAsync(tournamentId));
    }

    [Fact]
    public async Task RegisterTeam_AfterDeadline_Returns400AndDoesNotSaveTeam()
    {
        var tournamentId = await SeedTournamentAsync(startsAt: DateTimeOffset.UtcNow.AddMinutes(-1), maxTeams: 8);

        var response = await RegisterTeamAsync(tournamentId, "Spóźnieni");

        await AssertRegistrationBlockedAsync(response, expectedDetailFragment: "termin");
        Assert.Equal(0, await CountTeamsAsync(tournamentId));
    }

    [Fact]
    public async Task RegisterTeam_WhenTournamentStarted_Returns400AndDoesNotSaveTeam()
    {
        var tournamentId = await SeedTournamentAsync(startsAt: DateTimeOffset.UtcNow.AddDays(7), maxTeams: 8, started: true);

        var response = await RegisterTeamAsync(tournamentId, "Zbyt późno");

        await AssertRegistrationBlockedAsync(response, expectedDetailFragment: "rozpoczęty");
        Assert.Equal(0, await CountTeamsAsync(tournamentId));
    }

    [Fact]
    public async Task RegisterTeam_PastDeadlineAndFull_ReportsDeadline()
    {
        var tournamentId = await SeedTournamentAsync(startsAt: DateTimeOffset.UtcNow.AddMinutes(-1), maxTeams: 2, teamsCount: 2);

        var response = await RegisterTeamAsync(tournamentId, "Druga");

        await AssertRegistrationBlockedAsync(response, expectedDetailFragment: "termin");
    }

    private async Task<HttpResponseMessage> RegisterTeamAsync(Guid tournamentId, string name) =>
        await _client.PostAsJsonAsync("/api/v1/teams", new CreateTeamRequest(tournamentId, name, ContactEmail: null, Players: null));

    private static async Task AssertRegistrationBlockedAsync(HttpResponseMessage response, string expectedDetailFragment)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal("Rejestracja niedozwolona", problem.Title);
        Assert.Contains(expectedDetailFragment, problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<Guid> SeedTournamentAsync(DateTimeOffset startsAt, int maxTeams, bool started = false, int teamsCount = 0)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();

        var tournament = new Tournament($"Turniej {Guid.NewGuid():N}", "Piłka nożna", TournamentFormat.Cup, startsAt, maxTeams);

        for (var i = 1; i <= teamsCount; i++)
        {
            tournament.Teams.Add(new Team($"Drużyna {i}", tournament.Id));
        }

        if (started)
        {
            tournament.Start();
        }

        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();

        return tournament.Id;
    }

    private async Task<int> CountTeamsAsync(Guid tournamentId)
    {
        var teams = await _client.GetFromJsonAsync<List<TeamResponse>>($"/api/v1/teams/tournament/{tournamentId}");

        return teams!.Count;
    }
}
