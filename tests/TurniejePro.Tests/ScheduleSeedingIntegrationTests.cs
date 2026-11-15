using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using TurniejePro.Api.Auth;
using TurniejePro.Data;
using TurniejePro.Domain;
using TurniejePro.Domain.Enums;

namespace TurniejePro.Tests;

/// <summary>
/// Wybór rozstawienia drużyn w <c>POST /api/v1/tournaments/{id}/schedule</c> (KAN-302): body jest opcjonalne,
/// a rozstawienie wyznacza pary pierwszej rundy pucharu.
/// </summary>
public class ScheduleSeedingIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly DateTimeOffset FirstRegistration = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly Guid _ownerId = Guid.NewGuid();

    public ScheduleSeedingIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GenerateSchedule_WithoutBody_UsesRegistrationOrder()
    {
        var (tournamentId, teams) = await SeedCupAsync();

        var response = await PostScheduleAsync(tournamentId, body: null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal([(teams[0], teams[3]), (teams[1], teams[2])], FirstRound(tournamentId));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"seeding":"Registration"}""")]
    [InlineData("""{"seeding":"Registration","teamOrder":null}""")]
    public async Task GenerateSchedule_WithRegistrationSeedingOrEmptyObject_UsesRegistrationOrder(string body)
    {
        var (tournamentId, teams) = await SeedCupAsync();

        var response = await PostScheduleAsync(tournamentId, body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal([(teams[0], teams[3]), (teams[1], teams[2])], FirstRound(tournamentId));
    }

    [Fact]
    public async Task GenerateSchedule_RegistrationOrder_DoesNotDependOnTheOrderOfInsertingTeams()
    {
        var (tournamentId, teams) = await SeedCupAsync(insertInReverse: true);

        var response = await PostScheduleAsync(tournamentId, body: null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal([(teams[0], teams[3]), (teams[1], teams[2])], FirstRound(tournamentId));
    }

    [Fact]
    public async Task GenerateSchedule_WithManualSeeding_UsesTheGivenOrder()
    {
        var (tournamentId, teams) = await SeedCupAsync();
        var order = new[] { teams[2], teams[0], teams[3], teams[1] };

        var response = await PostScheduleAsync(tournamentId, ManualBody(order));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal([(teams[2], teams[1]), (teams[0], teams[3])], FirstRound(tournamentId));
    }

    [Fact]
    public async Task GenerateSchedule_WithRandomSeeding_PlaysEveryTeamInTheFirstRound()
    {
        var (tournamentId, teams) = await SeedCupAsync();

        var response = await PostScheduleAsync(tournamentId, """{"seeding":"Random"}""");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var inFirstRound = FirstRound(tournamentId).SelectMany(pair => new[] { pair.Home, pair.Away });
        Assert.Equal(teams.Order(), inFirstRound.Order());
    }

    [Fact]
    public async Task GenerateSchedule_ManualSeedingWithoutOrder_Returns400AndCreatesNothing()
    {
        var (tournamentId, _) = await SeedCupAsync();

        var response = await PostScheduleAsync(tournamentId, """{"seeding":"Manual"}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(MatchesOf(tournamentId));
    }

    [Fact]
    public async Task GenerateSchedule_ManualSeedingMissingATeam_Returns400AndCreatesNothing()
    {
        var (tournamentId, teams) = await SeedCupAsync();

        var response = await PostScheduleAsync(tournamentId, ManualBody([teams[0], teams[1], teams[2]]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(MatchesOf(tournamentId));
    }

    [Fact]
    public async Task GenerateSchedule_ManualSeedingWithRepeatedTeam_Returns400()
    {
        var (tournamentId, teams) = await SeedCupAsync();

        var response = await PostScheduleAsync(tournamentId, ManualBody([teams[0], teams[1], teams[2], teams[2]]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenerateSchedule_ManualSeedingWithTeamFromAnotherTournament_Returns400()
    {
        var (tournamentId, teams) = await SeedCupAsync();
        var (_, otherTeams) = await SeedCupAsync();

        var response = await PostScheduleAsync(tournamentId, ManualBody([teams[0], teams[1], teams[2], otherTeams[0]]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(MatchesOf(tournamentId));
    }

    [Theory]
    [InlineData("""{"seeding":"Banana"}""")]
    [InlineData("""{"seeding":99}""")]
    [InlineData("""{"seeding":0}""")]
    public async Task GenerateSchedule_WithUnknownSeeding_Returns400AndCreatesNothing(string body)
    {
        var (tournamentId, _) = await SeedCupAsync();

        var response = await PostScheduleAsync(tournamentId, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(MatchesOf(tournamentId));
    }

    [Fact]
    public async Task GenerateSchedule_ByNonOrganizerWithBody_Returns403AndCreatesNothing()
    {
        var (tournamentId, teams) = await SeedCupAsync();

        var response = await PostScheduleAsync(tournamentId, ManualBody(teams), Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(MatchesOf(tournamentId));
    }

    [Fact]
    public async Task GenerateSchedule_SecondTimeWithSeeding_Returns409Conflict()
    {
        var (tournamentId, _) = await SeedCupAsync();
        Assert.Equal(HttpStatusCode.Created, (await PostScheduleAsync(tournamentId, """{"seeding":"Random"}""")).StatusCode);

        var response = await PostScheduleAsync(tournamentId, """{"seeding":"Random"}""");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private static string ManualBody(IEnumerable<Guid> order) =>
        $$"""{"seeding":"Manual","teamOrder":[{{string.Join(',', order.Select(id => $"\"{id}\""))}}]}""";

    private async Task<HttpResponseMessage> PostScheduleAsync(Guid tournamentId, string? body, Guid? userId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<ITokenService>()
            .CreateToken(userId ?? _ownerId, "user@example.com", [Roles.User]).AccessToken;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tournaments/{tournamentId}/schedule");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        return await _client.SendAsync(request);
    }

    /// <summary>Turniej pucharowy ze czterema zaakceptowanymi drużynami zgłoszonymi co godzinę (zwraca ich Id w kolejności zgłoszeń).</summary>
    private async Task<(Guid TournamentId, List<Guid> Teams)> SeedCupAsync(bool insertInReverse = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();

        var tournament = new Tournament($"Puchar {Guid.NewGuid():N}", "Piłka nożna", TournamentFormat.Cup, DateTimeOffset.UtcNow.AddDays(7), 8, _ownerId);
        var teams = Enumerable.Range(0, 4)
            .Select(index =>
            {
                var team = new Team($"Drużyna {index + 1}", tournament.Id) { RegisteredAt = FirstRegistration.AddHours(index) };
                team.Accept();

                return team;
            })
            .ToList();

        db.Tournaments.Add(tournament);
        db.Teams.AddRange(insertInReverse ? Enumerable.Reverse(teams) : teams);
        await db.SaveChangesAsync();

        return (tournament.Id, teams.Select(team => team.Id).ToList());
    }

    private List<(Guid Home, Guid Away)> FirstRound(Guid tournamentId) =>
        MatchesOf(tournamentId)
            .Where(match => match.Round == 1)
            .OrderBy(match => match.Position)
            .Select(match => (match.HomeTeamId!.Value, match.AwayTeamId!.Value))
            .ToList();

    private List<Match> MatchesOf(Guid tournamentId)
    {
        using var scope = _factory.Services.CreateScope();

        return scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>().Matches.Where(match => match.TournamentId == tournamentId).ToList();
    }
}
