using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TurniejePro.Api.Auth;
using TurniejePro.Contracts.Teams;
using TurniejePro.Contracts.Tournaments;
using Xunit;

namespace TurniejePro.Tests;

public class TeamRegistrationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ITokenService _tokenService;

    public TeamRegistrationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();

        var scope = factory.Services.CreateScope();
        _tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
    }

    private void AuthenticateAs(string role)
    {
        var token = _tokenService.CreateToken(Guid.NewGuid(), "captain@example.com", new[] { role }).AccessToken;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<Guid> CreateTestTournamentAsync()
    {
        AuthenticateAs(Roles.Organizer);
        var createRequest = new CreateTournamentRequest(
            Name: $"Turniej Testowy {Guid.NewGuid().ToString()[..8]}",
            Discipline: "Piłka Nożna",
            Format: TournamentFormat.Cup,
            StartsAt: DateTimeOffset.UtcNow.AddDays(14),
            MaxTeams: 16
        );

        var response = await _client.PostAsJsonAsync("/api/v1/tournaments", createRequest);
        var created = await response.Content.ReadFromJsonAsync<TournamentDetailsResponse>();
        return created!.Id;
    }

    [Fact]
    public async Task RegisterTeam_WithPlayers_Returns201CreatedAndPendingStatus()
    {
        // Arrange
        var tournamentId = await CreateTestTournamentAsync();
        AuthenticateAs(Roles.Captain);

        var request = new CreateTeamRequest(
            TournamentId: tournamentId,
            Name: "Orły Warszawa",
            ContactEmail: "orly@warszawa.pl",
            Players: new List<CreatePlayerRequest>
            {
                new("Jan Kowalski", 10),
                new("Piotr Nowak", 1)
            }
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/teams", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var team = await response.Content.ReadFromJsonAsync<TeamResponse>();
        Assert.NotNull(team);
        Assert.Equal("Orły Warszawa", team.Name);
        Assert.Equal(TeamRegistrationStatus.Pending, team.Status);
        Assert.Equal(2, team.Players.Count);
    }

    [Fact]
    public async Task RegisterTeam_WithDuplicateNameInSameTournament_Returns400BadRequest()
    {
        // Arrange
        var tournamentId = await CreateTestTournamentAsync();
        AuthenticateAs(Roles.Captain);

        var request = new CreateTeamRequest(
            TournamentId: tournamentId,
            Name: "Husaria",
            ContactEmail: "kontakt@husaria.pl",
            Players: null
        );

        // Pierwsza rejestracja — sukces
        var firstResponse = await _client.PostAsJsonAsync("/api/v1/teams", request);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Act: Druga rejestracja tej samej nazwy
        var secondResponse = await _client.PostAsJsonAsync("/api/v1/teams", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
    }

    [Fact]
    public async Task GetTeamsByTournamentId_ReturnsAllRegisteredTeams()
    {
        // Arrange
        var tournamentId = await CreateTestTournamentAsync();
        AuthenticateAs(Roles.Captain);

        var request = new CreateTeamRequest(
            TournamentId: tournamentId,
            Name: "FC Smok",
            ContactEmail: "smok@krakow.pl",
            Players: null
        );
        await _client.PostAsJsonAsync("/api/v1/teams", request);

        // Act (Anonimowe pobranie listy)
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync($"/api/v1/teams/tournament/{tournamentId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var teams = await response.Content.ReadFromJsonAsync<List<TeamResponse>>();
        Assert.NotNull(teams);
        Assert.NotEmpty(teams);
        Assert.Contains(teams, t => t.Name == "FC Smok");
    }
}
