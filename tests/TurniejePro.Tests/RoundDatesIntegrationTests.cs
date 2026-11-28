using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using TurniejePro.Api.Auth;
using TurniejePro.Contracts.Matches;
using TurniejePro.Data;
using TurniejePro.Domain;
using TurniejePro.Domain.Enums;

namespace TurniejePro.Tests;

/// <summary>
/// Daty rund przez API (KAN-332): POST /schedule z datą 1. rundy i PUT /rounds/{round}/date.
/// </summary>
public class RoundDatesIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly DateTimeOffset FirstRound = DateTimeOffset.UtcNow.Date.AddDays(20).AddHours(10);

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ITokenService _tokenService;
    private readonly Guid _ownerId = Guid.NewGuid();

    public RoundDatesIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        _tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
    }

    private string Owner => Token(_ownerId, Roles.User);

    // --- Generowanie ---

    [Fact]
    public async Task Generate_WithFirstRoundDate_ReturnsDatedRounds_AndScheduleKeepsThem()
    {
        var id = await SeedTournamentAsync(teams: 4);

        using var response = await SendAsync(HttpMethod.Post, $"/api/v1/tournaments/{id}/schedule", Owner,
            new GenerateScheduleRequest(null, null, FirstRound, 5));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ScheduleResponse>();
        // Odstęp liczony w czasie polskim, więc oczekiwane daty też (test nie psuje się w okolicy zmiany czasu)
        Assert.Equal(
            [FirstRound, PolishTime.AddDays(FirstRound, 5), PolishTime.AddDays(FirstRound, 10)],
            created!.Rounds.Select(r => r.Date));
        Assert.All(created.Rounds, r => Assert.All(r.Matches, m => Assert.Equal(r.Date, m.ScheduledAt)));

        var schedule = await _client.GetFromJsonAsync<ScheduleResponse>($"/api/v1/tournaments/{id}/schedule");
        Assert.Equal(created.Rounds.Select(r => r.Date), schedule!.Rounds.Select(r => r.Date));
    }

    [Fact]
    public async Task Generate_WithoutBody_RoundsHaveNoDate()
    {
        var id = await SeedTournamentAsync(teams: 4);

        using var response = await SendAsync(HttpMethod.Post, $"/api/v1/tournaments/{id}/schedule", Owner);

        var created = await response.Content.ReadFromJsonAsync<ScheduleResponse>();
        Assert.All(created!.Rounds, r => Assert.Null(r.Date));
    }

    [Fact]
    public async Task Generate_FirstRoundBeforeRegistrationEnds_Returns400()
    {
        var id = await SeedTournamentAsync(teams: 4);

        using var response = await SendAsync(HttpMethod.Post, $"/api/v1/tournaments/{id}/schedule", Owner,
            new GenerateScheduleRequest(null, null, DateTimeOffset.UtcNow.AddDays(1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("koniec zapisów", problem!.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public async Task Generate_IntervalOutOfRange_Returns400(int days)
    {
        var id = await SeedTournamentAsync(teams: 4);

        using var response = await SendAsync(HttpMethod.Post, $"/api/v1/tournaments/{id}/schedule", Owner,
            new GenerateScheduleRequest(null, null, FirstRound, days));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Zmiana daty rundy ---

    [Fact]
    public async Task SetRoundDate_ByOwner_ChangesRoundAndIsVisibleInSchedule()
    {
        var id = await GenerateAsync();
        var newDate = FirstRound.AddDays(30);

        using var response = await SendAsync(HttpMethod.Put, $"/api/v1/tournaments/{id}/rounds/2/date", Owner, new SetRoundDateRequest(newDate));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var round = await response.Content.ReadFromJsonAsync<ScheduleRoundResponse>();
        Assert.Equal(2, round!.Round);
        Assert.Equal(newDate, round.Date);

        var schedule = await _client.GetFromJsonAsync<ScheduleResponse>($"/api/v1/tournaments/{id}/schedule");
        Assert.Equal(newDate, schedule!.Rounds.Single(r => r.Round == 2).Date);
        Assert.Equal(FirstRound, schedule.Rounds.Single(r => r.Round == 1).Date);
    }

    [Fact]
    public async Task SetRoundDate_Null_ClearsDate()
    {
        var id = await GenerateAsync();

        using var response = await SendAsync(HttpMethod.Put, $"/api/v1/tournaments/{id}/rounds/1/date", Owner, new SetRoundDateRequest(null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await response.Content.ReadFromJsonAsync<ScheduleRoundResponse>())!.Date);
    }

    [Fact]
    public async Task SetRoundDate_Errors()
    {
        var id = await GenerateAsync();
        var body = new SetRoundDateRequest(FirstRound.AddDays(1));

        using var anonymous = await SendAsync(HttpMethod.Put, $"/api/v1/tournaments/{id}/rounds/1/date", token: null, body);
        using var stranger = await SendAsync(HttpMethod.Put, $"/api/v1/tournaments/{id}/rounds/1/date", Token(Guid.NewGuid(), Roles.User), body);
        using var unknownRound = await SendAsync(HttpMethod.Put, $"/api/v1/tournaments/{id}/rounds/99/date", Owner, body);
        using var unknownTournament = await SendAsync(HttpMethod.Put, $"/api/v1/tournaments/{Guid.NewGuid()}/rounds/1/date", Owner, body);
        using var beforeRegistration = await SendAsync(HttpMethod.Put, $"/api/v1/tournaments/{id}/rounds/1/date", Owner,
            new SetRoundDateRequest(DateTimeOffset.UtcNow.AddDays(1)));

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, stranger.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownRound.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownTournament.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, beforeRegistration.StatusCode);
    }

    [Fact]
    public async Task SetRoundDate_RoundWithResult_Returns409()
    {
        var id = await GenerateAsync();
        var schedule = await _client.GetFromJsonAsync<ScheduleResponse>($"/api/v1/tournaments/{id}/schedule");
        var match = schedule!.Rounds[0].Matches[0];
        await SendAsync(HttpMethod.Put, $"/api/v1/tournaments/{id}/matches/{match.Id}/result", Owner, new UpdateMatchResultRequest(1, 0));

        using var response = await SendAsync(HttpMethod.Put, $"/api/v1/tournaments/{id}/rounds/1/date", Owner,
            new SetRoundDateRequest(FirstRound.AddDays(1)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task SetRoundDate_FinishedTournament_OwnerGets400_AdminCanChangeDateOfRoundWithoutResults()
    {
        // Zakończony turniej z rundą bez wyników (np. wolny los) – sztucznie, żeby sprawdzić samą regułę statusu
        Guid id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();
            var tournament = new Tournament($"Zakończony {Guid.NewGuid():N}", "football", TournamentFormat.Cup, DateTimeOffset.UtcNow.AddDays(14), 4, _ownerId);
            var team = new Team("Jedyna", tournament.Id);
            team.Accept();
            tournament.Teams.Add(team);
            db.Tournaments.Add(tournament);

            var bye = new Match(tournament.Id, 1, 1, team.Id);
            bye.MarkAsBye();
            db.Matches.Add(bye);
            tournament.Start();
            var finished = new Match(tournament.Id, 2, 1, Guid.NewGuid(), Guid.NewGuid());
            finished.SetResult(1, 0);
            tournament.CompleteIfAllMatchesFinished([finished]);

            await db.SaveChangesAsync();
            id = tournament.Id;
        }

        var body = new SetRoundDateRequest(FirstRound);
        using var owner = await SendAsync(HttpMethod.Put, $"/api/v1/tournaments/{id}/rounds/1/date", Owner, body);
        using var admin = await SendAsync(HttpMethod.Put, $"/api/v1/tournaments/{id}/rounds/1/date", Token(Guid.NewGuid(), Roles.Admin), body);

        Assert.Equal(HttpStatusCode.BadRequest, owner.StatusCode);
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
    }

    // --- Pomocnicze ---

    private string Token(Guid userId, string role) =>
        _tokenService.CreateToken(userId, $"{userId}@example.com", [role]).AccessToken;

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        return await _client.SendAsync(request);
    }

    private async Task<Guid> GenerateAsync()
    {
        var id = await SeedTournamentAsync(teams: 4);
        using var response = await SendAsync(HttpMethod.Post, $"/api/v1/tournaments/{id}/schedule", Owner,
            new GenerateScheduleRequest(null, null, FirstRound));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return id;
    }

    private async Task<Guid> SeedTournamentAsync(int teams)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();

        var tournament = new Tournament($"Turniej {Guid.NewGuid():N}", "football", TournamentFormat.League, DateTimeOffset.UtcNow.AddDays(14), 8, _ownerId);
        for (var i = 1; i <= teams; i++)
        {
            var team = new Team($"Drużyna {i}", tournament.Id);
            team.Accept();
            tournament.Teams.Add(team);
        }

        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();
        return tournament.Id;
    }
}
