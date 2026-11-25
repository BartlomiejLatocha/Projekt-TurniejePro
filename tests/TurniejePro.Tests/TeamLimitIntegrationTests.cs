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
using TeamRegistrationStatus = TurniejePro.Domain.Enums.TeamRegistrationStatus;

namespace TurniejePro.Tests;

/// <summary>
/// Limit drużyn w działającym API liczony bez odrzuconych zgłoszeń (KAN-280): odrzucone zgłoszenie zwalnia miejsce,
/// a oczekujące i zaakceptowane je zajmują.
/// </summary>
public class TeamLimitIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly Guid _organizerId = Guid.NewGuid();

    public TeamLimitIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RegisterTeam_WhenTheOtherSlotsAreTakenByRejectedTeams_Returns201()
    {
        var tournamentId = await SeedTournamentAsync(maxTeams: 2, TeamRegistrationStatus.Pending, TeamRegistrationStatus.Rejected, TeamRegistrationStatus.Rejected);

        var response = await RegisterTeamAsync(tournamentId, "Nowa drużyna");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task RegisterTeam_WhenAcceptedAndPendingTeamsFillTheLimit_Returns400()
    {
        var tournamentId = await SeedTournamentAsync(maxTeams: 2, TeamRegistrationStatus.Accepted, TeamRegistrationStatus.Pending, TeamRegistrationStatus.Rejected);

        var response = await RegisterTeamAsync(tournamentId, "Za dużo");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("limit", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegisterTeam_AfterOrganizerRejectsATeam_TheSlotIsFreeAgain()
    {
        var tournamentId = await SeedTournamentAsync(maxTeams: 2, TeamRegistrationStatus.Pending, TeamRegistrationStatus.Pending);
        Assert.Equal(HttpStatusCode.BadRequest, (await RegisterTeamAsync(tournamentId, "Trzecia")).StatusCode);

        var teamToReject = TeamsOf(tournamentId)[0];
        var rejection = await SendAsOrganizerAsync(HttpMethod.Patch, $"/api/v1/teams/{teamToReject}/reject");
        Assert.Equal(HttpStatusCode.OK, rejection.StatusCode);

        var response = await RegisterTeamAsync(tournamentId, "Trzecia");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<HttpResponseMessage> RegisterTeamAsync(Guid tournamentId, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/teams")
        {
            Content = JsonContent.Create(new CreateTeamRequest(tournamentId, name, ContactEmail: null, Players: TeamTestData.OnePlayer()))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", new TestTokens(_factory.Services).ForRoles(Roles.User));

        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAsOrganizerAsync(HttpMethod method, string url)
    {
        using var scope = _factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<ITokenService>()
            .CreateToken(_organizerId, "organizer@example.com", [Roles.User]).AccessToken;

        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await _client.SendAsync(request);
    }

    /// <summary>Turniej z podanym limitem i drużynami o wskazanych statusach (organizatorem jest <c>_organizerId</c>).</summary>
    private async Task<Guid> SeedTournamentAsync(int maxTeams, params TeamRegistrationStatus[] teamStatuses)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();

        var tournament = new Tournament(
            $"Turniej {Guid.NewGuid():N}",
            "Piłka nożna",
            TournamentFormat.League,
            DateTimeOffset.UtcNow.AddDays(7),
            maxTeams,
            _organizerId);

        for (var i = 0; i < teamStatuses.Length; i++)
        {
            var team = new Team($"Drużyna {i + 1}", tournament.Id);

            switch (teamStatuses[i])
            {
                case TeamRegistrationStatus.Accepted:
                    team.Accept();
                    break;
                case TeamRegistrationStatus.Rejected:
                    team.Reject();
                    break;
            }

            tournament.Teams.Add(team);
        }

        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();

        return tournament.Id;
    }

    private List<Guid> TeamsOf(Guid tournamentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();

        return db.Teams.Where(team => team.TournamentId == tournamentId).Select(team => team.Id).ToList();
    }
}
