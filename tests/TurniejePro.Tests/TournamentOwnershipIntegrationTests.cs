using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TurniejePro.Api.Auth;
using TurniejePro.Contracts.Matches;
using TurniejePro.Data;
using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using ContractFormat = TurniejePro.Contracts.Tournaments.TournamentFormat;
using UpdateTournamentRequest = TurniejePro.Contracts.Tournaments.UpdateTournamentRequest;

namespace TurniejePro.Tests;

/// <summary>
/// Własność turnieju na endpointach (KAN-283): organizator turnieju i administrator mogą nim zarządzać,
/// organizator innego turnieju i kapitan dostają 403, a nieistniejący turniej daje 404 przed 403.
/// </summary>
public class TournamentOwnershipIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ITokenService _tokenService;
    private readonly Guid _ownerId = Guid.NewGuid();

    public TournamentOwnershipIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        // ITokenService jest zarejestrowany jako scoped, więc pobieramy go z osobnego zakresu
        using var scope = factory.Services.CreateScope();
        _tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
    }

    private string Owner => Token(_ownerId, Roles.User);

    private string OtherOrganizer => Token(Guid.NewGuid(), Roles.User);

    private string Admin => Token(Guid.NewGuid(), Roles.Admin);

    private string Captain => Token(Guid.NewGuid(), Roles.User);

    // --- Edycja i usunięcie turnieju ---

    [Fact]
    public async Task UpdateTournament_ByOwner_Returns200()
    {
        var tournamentId = await SeedTournamentAsync(_ownerId);

        var response = await SendAsync(HttpMethod.Put, $"/api/v1/tournaments/{tournamentId}", Owner, ValidUpdate());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTournament_ByOtherOrganizer_Returns403AndDoesNotChangeIt()
    {
        var tournamentId = await SeedTournamentAsync(_ownerId);

        var response = await SendAsync(HttpMethod.Put, $"/api/v1/tournaments/{tournamentId}", OtherOrganizer, ValidUpdate());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(ValidUpdate().Name, Load<Tournament>(tournamentId)!.Name);
    }

    [Fact]
    public async Task UpdateTournament_ByAdmin_Returns200()
    {
        var tournamentId = await SeedTournamentAsync(_ownerId);

        var response = await SendAsync(HttpMethod.Put, $"/api/v1/tournaments/{tournamentId}", Admin, ValidUpdate());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTournament_ThatDoesNotExist_Returns404BeforeOwnershipCheck()
    {
        var response = await SendAsync(HttpMethod.Put, $"/api/v1/tournaments/{Guid.NewGuid()}", OtherOrganizer, ValidUpdate());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTournament_WithoutOrganizer_ByOrganizer_Returns403()
    {
        var tournamentId = await SeedTournamentAsync(organizerId: null);

        var response = await SendAsync(HttpMethod.Put, $"/api/v1/tournaments/{tournamentId}", Owner, ValidUpdate());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTournament_ByOtherOrganizer_Returns403AndKeepsIt()
    {
        var tournamentId = await SeedTournamentAsync(_ownerId);

        var response = await SendAsync(HttpMethod.Delete, $"/api/v1/tournaments/{tournamentId}", OtherOrganizer);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotNull(Load<Tournament>(tournamentId));
    }

    [Fact]
    public async Task DeleteTournament_ByOwner_Returns204()
    {
        var tournamentId = await SeedTournamentAsync(_ownerId);

        var response = await SendAsync(HttpMethod.Delete, $"/api/v1/tournaments/{tournamentId}", Owner);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // --- Akceptacja i odrzucenie zgłoszeń ---

    [Theory]
    [InlineData("accept")]
    [InlineData("reject")]
    public async Task DecideOnTeam_ByOwner_Returns200(string decision)
    {
        var tournamentId = await SeedTournamentAsync(_ownerId);
        var teamId = await SeedTeamAsync(tournamentId, accepted: false);

        var response = await SendAsync(HttpMethod.Patch, $"/api/v1/teams/{teamId}/{decision}", Owner);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("accept")]
    [InlineData("reject")]
    public async Task DecideOnTeam_ByOtherOrganizer_Returns403AndKeepsPending(string decision)
    {
        var tournamentId = await SeedTournamentAsync(_ownerId);
        var teamId = await SeedTeamAsync(tournamentId, accepted: false);

        var response = await SendAsync(HttpMethod.Patch, $"/api/v1/teams/{teamId}/{decision}", OtherOrganizer);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(TeamRegistrationStatus.Pending, Load<Team>(teamId)!.Status);
    }

    // --- Terminarz ---

    [Fact]
    public async Task GenerateSchedule_ByOwner_Returns201()
    {
        var tournamentId = await SeedTournamentWithAcceptedTeamsAsync(_ownerId);

        var response = await SendAsync(HttpMethod.Post, $"/api/v1/tournaments/{tournamentId}/schedule", Owner);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task GenerateSchedule_ByOtherOrganizer_Returns403AndCreatesNoMatches()
    {
        var tournamentId = await SeedTournamentWithAcceptedTeamsAsync(_ownerId);

        var response = await SendAsync(HttpMethod.Post, $"/api/v1/tournaments/{tournamentId}/schedule", OtherOrganizer);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(MatchesOf(tournamentId));
    }

    [Fact]
    public async Task DeleteSchedule_ByOtherOrganizer_Returns403AndKeepsMatches()
    {
        var (tournamentId, _) = await SeedTournamentWithMatchAsync(_ownerId);

        var response = await SendAsync(HttpMethod.Delete, $"/api/v1/tournaments/{tournamentId}/schedule", OtherOrganizer);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEmpty(MatchesOf(tournamentId));
    }

    [Fact]
    public async Task DeleteSchedule_ByOwner_Returns204()
    {
        var (tournamentId, _) = await SeedTournamentWithMatchAsync(_ownerId);

        var response = await SendAsync(HttpMethod.Delete, $"/api/v1/tournaments/{tournamentId}/schedule", Owner);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // --- Wyniki ---

    [Fact]
    public async Task SetResult_ByOwner_Returns200()
    {
        var (tournamentId, matchId) = await SeedTournamentWithMatchAsync(_ownerId);

        var response = await SendAsync(HttpMethod.Put, ResultUrl(tournamentId, matchId), Owner, new UpdateMatchResultRequest(2, 1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SetResult_ByOtherOrganizer_Returns403AndDoesNotSaveScore()
    {
        var (tournamentId, matchId) = await SeedTournamentWithMatchAsync(_ownerId);

        var response = await SendAsync(HttpMethod.Put, ResultUrl(tournamentId, matchId), OtherOrganizer, new UpdateMatchResultRequest(2, 1));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(Load<Match>(matchId)!.HomeScore);
    }

    [Fact]
    public async Task SetResult_ByCaptainOfTeamPlayingInMatch_Returns403AndDoesNotSaveScore()
    {
        // KAN-285: kapitan nie wpisuje wyniku nawet meczu własnej drużyny
        var captainId = Guid.NewGuid();
        var (tournamentId, matchId) = await SeedTournamentWithMatchAsync(_ownerId, homeCaptainId: captainId);

        var response = await SendAsync(HttpMethod.Put, ResultUrl(tournamentId, matchId), Token(captainId, Roles.User), new UpdateMatchResultRequest(3, 0));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(Load<Match>(matchId)!.HomeScore);
    }

    [Fact]
    public async Task SetResult_BySpectator_Returns403()
    {
        var (tournamentId, matchId) = await SeedTournamentWithMatchAsync(_ownerId);

        var response = await SendAsync(HttpMethod.Put, ResultUrl(tournamentId, matchId), Token(Guid.NewGuid(), Roles.User), new UpdateMatchResultRequest(2, 1));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task EnterResultsPolicy_AllowsOnlyOrganizerAndAdmin()
    {
        var tournament = new Tournament("Turniej", "Piłka nożna", TournamentFormat.League, DateTimeOffset.UtcNow.AddDays(7), 8, _ownerId);
        using var scope = _factory.Services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        async Task<bool> CanEnterResults(Guid userId, string role) =>
            (await authorization.AuthorizeAsync(Principal(userId, role), tournament, Policies.EnterResults)).Succeeded;

        Assert.True(await CanEnterResults(_ownerId, Roles.User));
        Assert.True(await CanEnterResults(Guid.NewGuid(), Roles.Admin));
        Assert.False(await CanEnterResults(Guid.NewGuid(), Roles.User));
    }

    [Fact]
    public async Task SetResult_ByCaptain_Returns403()
    {
        var (tournamentId, matchId) = await SeedTournamentWithMatchAsync(_ownerId);

        var response = await SendAsync(HttpMethod.Put, ResultUrl(tournamentId, matchId), Captain, new UpdateMatchResultRequest(2, 1));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SetResult_ByAdmin_Returns200()
    {
        var (tournamentId, matchId) = await SeedTournamentWithMatchAsync(_ownerId);

        var response = await SendAsync(HttpMethod.Put, ResultUrl(tournamentId, matchId), Admin, new UpdateMatchResultRequest(2, 1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SetResult_InTournamentThatDoesNotExist_Returns404()
    {
        var response = await SendAsync(HttpMethod.Put, ResultUrl(Guid.NewGuid(), Guid.NewGuid()), OtherOrganizer, new UpdateMatchResultRequest(2, 1));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Pomocnicze ---

    private static string ResultUrl(Guid tournamentId, Guid matchId) =>
        $"/api/v1/tournaments/{tournamentId}/matches/{matchId}/result";

    private static UpdateTournamentRequest ValidUpdate() =>
        new("Turniej po zmianie", "Siatkówka", ContractFormat.League, DateTimeOffset.UtcNow.AddDays(14), 8);

    private string Token(Guid userId, string role) =>
        _tokenService.CreateToken(userId, $"{userId}@example.com", [role]).AccessToken;

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        return await _client.SendAsync(request);
    }

    private async Task<Guid> SeedTournamentAsync(Guid? organizerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();

        var tournament = new Tournament($"Turniej {Guid.NewGuid()}", "Piłka nożna", TournamentFormat.League, DateTimeOffset.UtcNow.AddDays(7), 8, organizerId);
        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();

        return tournament.Id;
    }

    private static ClaimsPrincipal Principal(Guid userId, string role) =>
        new(new ClaimsIdentity(
            [new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()), new Claim("role", role)],
            "Test",
            "name",
            "role"));

    private async Task<Guid> SeedTeamAsync(Guid tournamentId, bool accepted, Guid? captainId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();

        var team = new Team($"Drużyna {Guid.NewGuid().ToString()[..8]}", tournamentId, captainId);
        if (accepted)
        {
            team.Accept();
        }

        db.Teams.Add(team);
        await db.SaveChangesAsync();

        return team.Id;
    }

    private async Task<Guid> SeedTournamentWithAcceptedTeamsAsync(Guid organizerId)
    {
        var tournamentId = await SeedTournamentAsync(organizerId);
        for (var i = 0; i < 4; i++)
        {
            await SeedTeamAsync(tournamentId, accepted: true);
        }

        return tournamentId;
    }

    private async Task<(Guid TournamentId, Guid MatchId)> SeedTournamentWithMatchAsync(Guid organizerId, Guid? homeCaptainId = null)
    {
        var tournamentId = await SeedTournamentAsync(organizerId);
        var homeId = await SeedTeamAsync(tournamentId, accepted: true, homeCaptainId);
        var awayId = await SeedTeamAsync(tournamentId, accepted: true);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();

        var match = new Match(tournamentId, 1, 1, homeId, awayId);
        db.Matches.Add(match);

        // Turniej z terminarzem jest rozpoczęty (KAN-290), a wyniki można wpisywać tylko w rozpoczętym turnieju
        var tournament = await db.Tournaments.FindAsync(tournamentId);
        tournament!.Start();
        await db.SaveChangesAsync();

        return (tournamentId, match.Id);
    }

    private T? Load<T>(Guid id) where T : class
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();
        return db.Set<T>().Find(id);
    }

    private List<Match> MatchesOf(Guid tournamentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();
        return db.Matches.Where(m => m.TournamentId == tournamentId).ToList();
    }
}
