using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TurniejePro.Api.Auth;
using TurniejePro.Contracts.Profile;
using TurniejePro.Data;
using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using ContractStatus = TurniejePro.Contracts.Tournaments.TournamentStatus;
using ContractTeamStatus = TurniejePro.Contracts.Teams.TeamRegistrationStatus;

namespace TurniejePro.Tests;

/// <summary>
/// Endpointy „Moje turnieje” i „Moje drużyny” (KAN-304): każdy widzi tylko swoje zasoby, także szkice,
/// posortowane od tych, które wymagają uwagi.
/// </summary>
public class MeEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ITokenService _tokenService;

    public MeEndpointsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        _tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
    }

    [Theory]
    [InlineData("/api/v1/me/tournaments")]
    [InlineData("/api/v1/me/teams")]
    public async Task WithoutToken_Returns401(string url)
    {
        using var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/me/tournaments")]
    [InlineData("/api/v1/me/teams")]
    public async Task NewUser_GetsEmptyList(string url)
    {
        using var response = await GetAsync(url, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<List<object>>())!);
    }

    [Fact]
    public async Task MyTournaments_ReturnsOnlyOwnTournaments_IncludingDrafts_InAttentionOrder()
    {
        var organizerId = Guid.NewGuid();
        await SeedTournamentAsync(Guid.NewGuid(), "Cudzy", TournamentStatus.RegistrationOpen);
        await SeedTournamentAsync(organizerId, "Zakończony", TournamentStatus.Finished);
        await SeedTournamentAsync(organizerId, "Szkic", TournamentStatus.Draft);
        await SeedTournamentAsync(organizerId, "Zapisy", TournamentStatus.RegistrationOpen);
        await SeedTournamentAsync(organizerId, "W trakcie", TournamentStatus.InProgress);

        var result = await GetListAsync<MyTournamentResponse>("/api/v1/me/tournaments", organizerId);

        Assert.Equal(["W trakcie", "Zapisy", "Szkic", "Zakończony"], result.Select(t => t.Name));
        Assert.Equal(
            [ContractStatus.InProgress, ContractStatus.RegistrationOpen, ContractStatus.Draft, ContractStatus.Finished],
            result.Select(t => t.Status));
    }

    [Fact]
    public async Task MyTournaments_CountsAcceptedAndPendingTeams_WithoutRejected()
    {
        var organizerId = Guid.NewGuid();
        var tournamentId = await SeedTournamentAsync(organizerId, "Zapisy", TournamentStatus.RegistrationOpen);
        await SeedTeamAsync(tournamentId, "A", captainId: null, TeamRegistrationStatus.Accepted);
        await SeedTeamAsync(tournamentId, "B", captainId: null, TeamRegistrationStatus.Accepted);
        await SeedTeamAsync(tournamentId, "C", captainId: null, TeamRegistrationStatus.Pending);
        await SeedTeamAsync(tournamentId, "D", captainId: null, TeamRegistrationStatus.Rejected);

        var tournament = Assert.Single(await GetListAsync<MyTournamentResponse>("/api/v1/me/tournaments", organizerId));

        Assert.Equal(2, tournament.AcceptedTeams);
        Assert.Equal(1, tournament.PendingTeams);
        Assert.Equal(8, tournament.MaxTeams);
        Assert.Equal("football", tournament.Discipline);
    }

    [Fact]
    public async Task MyTeams_ReturnsOnlyOwnTeams_WithTournamentData_InAttentionOrder()
    {
        var captainId = Guid.NewGuid();
        var finished = await SeedTournamentAsync(Guid.NewGuid(), "Liga zakończona", TournamentStatus.Finished);
        var registration = await SeedTournamentAsync(Guid.NewGuid(), "Puchar z zapisami", TournamentStatus.RegistrationOpen);
        await SeedTeamAsync(finished, "Orły", captainId, TeamRegistrationStatus.Accepted, players: 3);
        await SeedTeamAsync(registration, "Sokoły", captainId, TeamRegistrationStatus.Pending);
        await SeedTeamAsync(registration, "Cudza drużyna", Guid.NewGuid(), TeamRegistrationStatus.Pending);

        var result = await GetListAsync<MyTeamResponse>("/api/v1/me/teams", captainId);

        Assert.Equal(["Sokoły", "Orły"], result.Select(t => t.Name));

        var pending = result[0];
        Assert.Equal(ContractTeamStatus.Pending, pending.Status);
        Assert.Equal(registration, pending.TournamentId);
        Assert.Equal("Puchar z zapisami", pending.TournamentName);
        Assert.Equal(ContractStatus.RegistrationOpen, pending.TournamentStatus);

        var accepted = result[1];
        Assert.Equal(ContractTeamStatus.Accepted, accepted.Status);
        Assert.Equal(3, accepted.PlayersCount);
        Assert.False(accepted.HasLogo);
        Assert.Equal(ContractStatus.Finished, accepted.TournamentStatus);
    }

    [Fact]
    public async Task OrganizerWhoRegisteredTeamInOwnTournament_SeesItInBothLists()
    {
        var userId = Guid.NewGuid();
        var tournamentId = await SeedTournamentAsync(userId, "Własny turniej", TournamentStatus.RegistrationOpen);
        await SeedTeamAsync(tournamentId, "Własna drużyna", userId, TeamRegistrationStatus.Pending);

        var tournaments = await GetListAsync<MyTournamentResponse>("/api/v1/me/tournaments", userId);
        var teams = await GetListAsync<MyTeamResponse>("/api/v1/me/teams", userId);

        Assert.Equal(1, Assert.Single(tournaments).PendingTeams);
        Assert.Equal("Własny turniej", Assert.Single(teams).TournamentName);
    }

    [Fact]
    public async Task Admin_SeesOnlyOwnResources()
    {
        await SeedTournamentAsync(Guid.NewGuid(), "Cudzy", TournamentStatus.Draft);

        var result = await GetListAsync<MyTournamentResponse>("/api/v1/me/tournaments", Guid.NewGuid(), Roles.Admin);

        Assert.Empty(result);
    }

    // --- Pomocnicze ---

    private async Task<HttpResponseMessage> GetAsync(string url, Guid userId, string role = Roles.User)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var token = _tokenService.CreateToken(userId, $"{userId}@example.com", [role]).AccessToken;
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<List<T>> GetListAsync<T>(string url, Guid userId, string role = Roles.User)
    {
        using var response = await GetAsync(url, userId, role);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<List<T>>();
        Assert.NotNull(result);
        return result;
    }

    private async Task<Guid> SeedTournamentAsync(Guid organizerId, string name, TournamentStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();

        var tournament = new Tournament(name, "football", TournamentFormat.League, DateTimeOffset.UtcNow.AddDays(14), 8, organizerId);
        var match = new Match(tournament.Id, 1, 1, null, null);
        switch (status)
        {
            case TournamentStatus.RegistrationOpen:
                tournament.OpenRegistration();
                break;
            case TournamentStatus.InProgress:
                tournament.Start();
                break;
            case TournamentStatus.Finished:
                tournament.Start();
                match.SetResult(1, 0);
                tournament.CompleteIfAllMatchesFinished([match]);
                break;
        }

        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();
        return tournament.Id;
    }

    private async Task SeedTeamAsync(Guid tournamentId, string name, Guid? captainId, TeamRegistrationStatus status, int players = 0)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();

        var team = new Team(name, tournamentId, captainId);
        for (var i = 1; i <= players; i++)
        {
            team.AddPlayer($"Zawodnik {i}", i);
        }

        if (status == TeamRegistrationStatus.Accepted)
        {
            team.Accept();
        }
        else if (status == TeamRegistrationStatus.Rejected)
        {
            team.Reject();
        }

        db.Teams.Add(team);
        await db.SaveChangesAsync();
    }
}
