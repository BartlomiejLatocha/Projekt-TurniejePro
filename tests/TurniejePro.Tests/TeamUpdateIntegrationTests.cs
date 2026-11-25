using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TurniejePro.Api.Auth;
using TurniejePro.Contracts.Teams;
using TurniejePro.Contracts.Tournaments;
using Xunit;

namespace TurniejePro.Tests;

public class TeamUpdateIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ITokenService _tokenService;

    public TeamUpdateIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();

        var scope = factory.Services.CreateScope();
        _tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
    }

    private readonly Guid _organizerId = Guid.NewGuid();
    private readonly Guid _captainId = Guid.NewGuid();
    private readonly Guid _strangerId = Guid.NewGuid();

    private void AuthenticateAs(Guid userId)
    {
        var token = _tokenService.CreateToken(userId, "user@example.com", new[] { Roles.User }).AccessToken;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private sealed record PlayerDto(string FullName, int? JerseyNumber);

    private static PlayerDto[] Players(int count) =>
        Enumerable.Range(1, count).Select(i => new PlayerDto($"Zawodnik {i}", null)).ToArray();

    // Organizator tworzy turniej, kapitan zgłasza drużynę z dwoma zawodnikami
    private async Task<Guid> SetupTeamAsync()
    {
        AuthenticateAs(_organizerId);
        var tournamentRes = await _client.PostAsJsonAsync("/api/v1/tournaments", new CreateTournamentRequest(
            Name: $"Edycja {Guid.NewGuid():N}"[..20],
            Discipline: "volleyball",
            Format: TournamentFormat.Cup,
            StartsAt: DateTimeOffset.UtcNow.AddDays(7),
            MaxTeams: 8));
        var tournament = await tournamentRes.Content.ReadFromJsonAsync<TournamentDetailsResponse>();

        AuthenticateAs(_captainId);
        var teamRes = await _client.PostAsJsonAsync("/api/v1/teams", new CreateTeamRequest(
            TournamentId: tournament!.Id,
            Name: $"Druzyna {Guid.NewGuid():N}"[..16],
            ContactEmail: "kapitan@example.com",
            Players: [new("Jan Kowalski", 10), new("Adam Nowak", 11)]));
        var team = await teamRes.Content.ReadFromJsonAsync<TeamResponse>();
        return team!.Id;
    }

    [Fact]
    public async Task Update_AsCaptain_ReplacesRosterAndReturns200()
    {
        var teamId = await SetupTeamAsync();
        AuthenticateAs(_captainId);

        var response = await _client.PutAsJsonAsync($"/api/v1/teams/{teamId}", new
        {
            name = "Nowa nazwa",
            contactEmail = "nowy@example.com",
            players = Players(3)
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<TeamResponse>();
        Assert.Equal("Nowa nazwa", updated!.Name);
        Assert.Equal(3, updated.Players.Count);
    }

    [Fact]
    public async Task Update_WithNoPlayers_Returns400()
    {
        var teamId = await SetupTeamAsync();
        AuthenticateAs(_captainId);

        var response = await _client.PutAsJsonAsync($"/api/v1/teams/{teamId}", new
        {
            name = "Druzyna bez skladu",
            contactEmail = (string?)null,
            players = Players(0)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithMoreThanMaximumPlayers_Returns400()
    {
        var teamId = await SetupTeamAsync();
        AuthenticateAs(_captainId);

        var response = await _client.PutAsJsonAsync($"/api/v1/teams/{teamId}", new
        {
            name = "Za duza druzyna",
            contactEmail = (string?)null,
            players = Players(31)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithExactlyMaximumPlayers_Returns200()
    {
        var teamId = await SetupTeamAsync();
        AuthenticateAs(_captainId);

        var response = await _client.PutAsJsonAsync($"/api/v1/teams/{teamId}", new
        {
            name = "Pelna druzyna",
            contactEmail = (string?)null,
            players = Players(30)
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsOrganizer_Returns200()
    {
        var teamId = await SetupTeamAsync();
        AuthenticateAs(_organizerId);

        var response = await _client.PutAsJsonAsync($"/api/v1/teams/{teamId}", new
        {
            name = "Poprawka organizatora",
            contactEmail = (string?)null,
            players = Players(2)
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsStranger_Returns403()
    {
        var teamId = await SetupTeamAsync();
        AuthenticateAs(_strangerId);

        var response = await _client.PutAsJsonAsync($"/api/v1/teams/{teamId}", new
        {
            name = "Obca zmiana",
            contactEmail = (string?)null,
            players = Players(2)
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
