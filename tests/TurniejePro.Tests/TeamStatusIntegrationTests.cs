using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TurniejePro.Api.Auth;
using TurniejePro.Contracts.Teams;
using TurniejePro.Contracts.Tournaments;
using Xunit;

namespace TurniejePro.Tests;

public class TeamStatusIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ITokenService _tokenService;

    public TeamStatusIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();

        var scope = factory.Services.CreateScope();
        _tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
    }

    private void AuthenticateAs(string role)
    {
        var token = _tokenService.CreateToken(Guid.NewGuid(), "user@example.com", new[] { role }).AccessToken;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<(Guid tournamentId, Guid teamId)> SetupTournamentAndTeamAsync()
    {
        // 1. Stwórz turniej jako Organizator
        AuthenticateAs(Roles.Organizer);
        var createTournamentReq = new CreateTournamentRequest(
            Name: $"Turniej Status {Guid.NewGuid().ToString()[..8]}",
            Discipline: "Siatkówka",
            Format: TournamentFormat.Cup,
            StartsAt: DateTimeOffset.UtcNow.AddDays(7),
            MaxTeams: 2
        );
        var tourRes = await _client.PostAsJsonAsync("/api/v1/tournaments", createTournamentReq);
        var tournament = await tourRes.Content.ReadFromJsonAsync<TournamentDetailsResponse>();

        // 2. Zgłoś drużynę jako Kapitan
        AuthenticateAs(Roles.Captain);
        var createTeamReq = new CreateTeamRequest(
            TournamentId: tournament!.Id,
            Name: "Team Test Status",
            ContactEmail: "test@team.pl",
            Players: null
        );
        var teamRes = await _client.PostAsJsonAsync("/api/v1/teams", createTeamReq);
        var team = await teamRes.Content.ReadFromJsonAsync<TeamResponse>();

        return (tournament.Id, team!.Id);
    }

    [Fact]
    public async Task AcceptTeam_AsOrganizer_Returns200OkAndAcceptedStatus()
    {
        // Arrange
        var (_, teamId) = await SetupTournamentAndTeamAsync();
        AuthenticateAs(Roles.Organizer);

        // Act
        var response = await _client.PatchAsync($"/api/v1/teams/{teamId}/accept", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var team = await response.Content.ReadFromJsonAsync<TeamResponse>();
        Assert.NotNull(team);
        Assert.Equal(TeamRegistrationStatus.Accepted, team.Status);
    }

    [Fact]
    public async Task AcceptTeam_AsCaptain_Returns403Forbidden()
    {
        // Arrange
        var (_, teamId) = await SetupTournamentAndTeamAsync();
        AuthenticateAs(Roles.Captain); // Kapitan nie ma uprawnień do decyzji Organizatora

        // Act
        var response = await _client.PatchAsync($"/api/v1/teams/{teamId}/accept", null);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RejectTeam_AsOrganizer_Returns200OkAndRejectedStatus()
    {
        // Arrange
        var (_, teamId) = await SetupTournamentAndTeamAsync();
        AuthenticateAs(Roles.Organizer);

        // Act
        var response = await _client.PatchAsync($"/api/v1/teams/{teamId}/reject", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var team = await response.Content.ReadFromJsonAsync<TeamResponse>();
        Assert.NotNull(team);
        Assert.Equal(TeamRegistrationStatus.Rejected, team.Status);
    }
}
