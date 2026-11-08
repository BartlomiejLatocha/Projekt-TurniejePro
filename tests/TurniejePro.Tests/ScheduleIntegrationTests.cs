using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TurniejePro.Contracts.Matches;
using TurniejePro.Data;
using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using Xunit;

namespace TurniejePro.Tests;

public class ScheduleIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly TestTokens _testTokens;

    public ScheduleIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _testTokens = new TestTokens(factory.Services);
    }

    [Theory]
    [InlineData(TournamentFormat.League, 4, 6, 3)]
    [InlineData(TournamentFormat.League, 5, 10, 5)]
    [InlineData(TournamentFormat.Cup, 4, 3, 2)]
    [InlineData(TournamentFormat.Cup, 5, 7, 3)]
    public async Task POST_GenerateSchedule_ValidScenarios_Returns201CreatedAndCorrectStructure(
        TournamentFormat format, int teamCount, int expectedMatches, int expectedRounds)
    {
        // Arrange
        var token = _testTokens.ForRoles("Admin", "Organizer");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var tournamentId = await SeedTournamentWithTeamsAsync(format, teamCount);

        // Act
        var response = await _client.PostAsync($"/api/v1/tournaments/{tournamentId}/schedule", null);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var schedule = await response.Content.ReadFromJsonAsync<ScheduleResponse>();
        Assert.NotNull(schedule);
        Assert.Equal(tournamentId, schedule!.TournamentId);
        Assert.Equal(expectedRounds, schedule.Rounds.Count);
        Assert.Equal(expectedMatches, schedule.Rounds.Sum(r => r.Matches.Count));
    }

    [Fact]
    public async Task POST_GenerateSchedule_SecondTime_Returns409Conflict()
    {
        // Arrange
        var token = _testTokens.ForRoles("Admin", "Organizer");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var tournamentId = await SeedTournamentWithTeamsAsync(TournamentFormat.League, 4);

        // Pierwsze wygenerowanie
        await _client.PostAsync($"/api/v1/tournaments/{tournamentId}/schedule", null);

        // Act: Druga próba generowania
        var secondResponse = await _client.PostAsync($"/api/v1/tournaments/{tournamentId}/schedule", null);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task POST_GenerateSchedule_LessThan2Teams_Returns400BadRequest()
    {
        // Arrange
        var token = _testTokens.ForRoles("Admin", "Organizer");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var tournamentId = await SeedTournamentWithTeamsAsync(TournamentFormat.League, 1);

        // Act
        var response = await _client.PostAsync($"/api/v1/tournaments/{tournamentId}/schedule", null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GET_Schedule_BeforeAndAfterGeneration_Returns404And200()
    {
        // Arrange
        var tournamentId = await SeedTournamentWithTeamsAsync(TournamentFormat.League, 4);

        // 1. GET przed wygenerowaniem terminarza -> 404
        var beforeResponse = await _client.GetAsync($"/api/v1/tournaments/{tournamentId}/schedule");
        Assert.Equal(HttpStatusCode.NotFound, beforeResponse.StatusCode);

        // Generowanie terminarza z prawami Admina/Organizatora
        var token = _testTokens.ForRoles("Admin", "Organizer");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await _client.PostAsync($"/api/v1/tournaments/{tournamentId}/schedule", null);

        // 2. GET po wygenerowaniu -> 200 OK (GET jest dostępny publicznie [AllowAnonymous])
        _client.DefaultRequestHeaders.Authorization = null;
        var afterResponse = await _client.GetAsync($"/api/v1/tournaments/{tournamentId}/schedule");

        Assert.Equal(HttpStatusCode.OK, afterResponse.StatusCode);
        var schedule = await afterResponse.Content.ReadFromJsonAsync<ScheduleResponse>();
        Assert.NotNull(schedule);
        Assert.Equal(3, schedule!.Rounds.Count);
    }

    [Fact]
    public async Task DELETE_Schedule_WithAndWithoutSchedule_Returns204And404()
    {
        // Arrange
        var token = _testTokens.ForRoles("Admin", "Organizer");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var tournamentId = await SeedTournamentWithTeamsAsync(TournamentFormat.League, 4);

        // 1. DELETE bez terminarza -> 404
        var deleteWithoutResponse = await _client.DeleteAsync($"/api/v1/tournaments/{tournamentId}/schedule");
        Assert.Equal(HttpStatusCode.NotFound, deleteWithoutResponse.StatusCode);

        // Utworzenie terminarza
        await _client.PostAsync($"/api/v1/tournaments/{tournamentId}/schedule", null);

        // 2. DELETE z terminarzem -> 204 NoContent
        var deleteWithResponse = await _client.DeleteAsync($"/api/v1/tournaments/{tournamentId}/schedule");
        Assert.Equal(HttpStatusCode.NoContent, deleteWithResponse.StatusCode);

        // Weryfikacja usunięcia poprzez GET -> 404
        var getAfterDelete = await _client.GetAsync($"/api/v1/tournaments/{tournamentId}/schedule");
        Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);
    }

    [Fact]
    public async Task POST_And_DELETE_Schedule_AuthorizationChecks()
    {
        var tournamentId = await SeedTournamentWithTeamsAsync(TournamentFormat.League, 4);

        // 1. Brak tokena -> 401 Unauthorized
        _client.DefaultRequestHeaders.Authorization = null;
        var unauthResponse = await _client.PostAsync($"/api/v1/tournaments/{tournamentId}/schedule", null);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthResponse.StatusCode);

        // 2. Token kibica/zwykłego użytkownika bez wymaganych ról -> 403 Forbidden
        var fanToken = _testTokens.ForRoles("Fan");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fanToken);
        var forbiddenResponse = await _client.PostAsync($"/api/v1/tournaments/{tournamentId}/schedule", null);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
    }

    [Fact]
    public async Task AcceptTeamRegistration_AfterScheduleGenerated_Returns409Conflict()
    {
        // Arrange
        var token = _testTokens.ForRoles("Admin", "Organizer");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var tournamentId = await SeedTournamentWithTeamsAsync(TournamentFormat.League, 4);

        // Generujemy terminarz
        await _client.PostAsync($"/api/v1/tournaments/{tournamentId}/schedule", null);

        // Dodajemy nową oczekującą drużynę
        var newTeamId = await SeedPendingTeamAsync(tournamentId);

        // Act: Próba akceptacji zgłoszenia po wygenerowaniu terminarza
        var response = await _client.PatchAsync($"/api/v1/teams/{newTeamId}/accept", null);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private async Task<Guid> SeedTournamentWithTeamsAsync(TournamentFormat format, int teamCount)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();

        var tournament = new Tournament($"Turniej {Guid.NewGuid()}", "Opis turnieju", format, DateTimeOffset.UtcNow.AddDays(7), 16, null);
        db.Tournaments.Add(tournament);

        for (int i = 1; i <= teamCount; i++)
        {
            var team = new Team($"Drużyna {i}", tournament.Id);
            team.Accept();
            db.Teams.Add(team);
        }

        await db.SaveChangesAsync();
        return tournament.Id;
    }

    private async Task<Guid> SeedPendingTeamAsync(Guid tournamentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();

        var team = new Team("Dodatkowa Drużyna", tournamentId);
        db.Teams.Add(team);
        await db.SaveChangesAsync();
        return team.Id;
    }
}
