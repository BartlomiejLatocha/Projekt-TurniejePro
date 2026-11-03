using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using TurniejePro.Api.Auth;
using TurniejePro.Contracts.Tournaments;

namespace TurniejePro.Tests;

/// <summary>
/// Sprawdza, że walidacja z <see cref="TournamentRequestValidationTests"/> działa tak samo w prawdziwym API:
/// niepoprawne żądanie kończy się kodem 400 i błędem przy właściwym polu (KAN-70).
/// </summary>
public class TournamentValidationApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TournamentValidationApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", new TestTokens(factory.Services).ForRoles(Roles.Organizer));
    }

    [Fact]
    public async Task CreateTournament_WithTooShortName_Returns400WithNameError()
    {
        var request = new CreateTournamentRequest("AB", "Szachy", TournamentFormat.Cup, DateTimeOffset.UtcNow.AddDays(7), 8);

        var response = await _client.PostAsJsonAsync("/api/v1/tournaments", request);

        await AssertBadRequestWithErrorsOnlyForAsync(response, "Name");
    }

    [Fact]
    public async Task CreateTournament_WithSeveralInvalidFields_Returns400WithErrorForEachField()
    {
        var request = new CreateTournamentRequest("AB", string.Empty, TournamentFormat.Cup, DateTimeOffset.UtcNow.AddDays(7), 1);

        var response = await _client.PostAsJsonAsync("/api/v1/tournaments", request);

        await AssertBadRequestWithErrorsOnlyForAsync(response, "Name", "Discipline", "MaxTeams");
    }

    [Fact]
    public async Task UpdateTournament_WithInvalidMaxTeams_Returns400BeforeLookingUpTheTournament()
    {
        var request = new UpdateTournamentRequest("Liga Szkolna", "Szachy", TournamentFormat.Cup, DateTimeOffset.UtcNow.AddDays(7), 129);

        // Turniej o tym Id nie istnieje, a odpowiedź i tak to 400, a nie 404: walidacja modelu działa przed akcją.
        var response = await _client.PutAsJsonAsync($"/api/v1/tournaments/{Guid.NewGuid()}", request);

        await AssertBadRequestWithErrorsOnlyForAsync(response, "MaxTeams");
    }

    private static async Task AssertBadRequestWithErrorsOnlyForAsync(HttpResponseMessage response, params string[] fields)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.NotNull(problem);

        // Wielkość liter w kluczach zależy od ustawień serializacji, więc porównujemy bez jej rozróżniania
        var reportedFields = problem.Errors.Keys.Order(StringComparer.OrdinalIgnoreCase);
        Assert.Equal(fields.Order(StringComparer.OrdinalIgnoreCase), reportedFields, StringComparer.OrdinalIgnoreCase);
    }
}
