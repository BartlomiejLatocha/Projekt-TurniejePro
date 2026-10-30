using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TurniejePro.Api.Auth;
using TurniejePro.Contracts.Common;
using TurniejePro.Contracts.Tournaments;
using Xunit;

namespace TurniejePro.Tests;

public class TournamentsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ITokenService _tokenService;

    public TournamentsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();

        var scope = factory.Services.CreateScope();
        _tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
    }

    private void AuthenticateAs(string role)
    {
        var token = _tokenService.CreateToken(Guid.NewGuid(), "test@example.com", new[] { role }).AccessToken;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task CreateTournament_WithValidData_Returns201CreatedAndLocationHeader()
    {
        // Arrange
        AuthenticateAs(Roles.Organizer);
        var request = new CreateTournamentRequest(
            Name: "E-Sport Champions League",
            Discipline: "Valorant",
            Format: TournamentFormat.Cup,
            StartsAt: DateTimeOffset.UtcNow.AddDays(7),
            MaxTeams: 16
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/tournaments", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var createdTournament = await response.Content.ReadFromJsonAsync<TournamentDetailsResponse>();
        Assert.NotNull(createdTournament);
        Assert.Equal(request.Name, createdTournament.Name);
        Assert.Equal(request.MaxTeams, createdTournament.MaxTeams);
    }

    [Fact]
    public async Task CreateTournament_WithInvalidData_Returns400BadRequest()
    {
        // Arrange: MaxTeams < 2 (złamana walidacja DTO/domeny)
        AuthenticateAs(Roles.Organizer);
        var request = new CreateTournamentRequest(
            Name: "AB", // Za krótka nazwa (min. 3 znaki)
            Discipline: "LOL",
            Format: TournamentFormat.Cup,
            StartsAt: DateTimeOffset.UtcNow,
            MaxTeams: 1
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/tournaments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPagedTournaments_Returns200OkWithPagedResponse()
    {
        // Act (Dostęp anonimowy)
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/v1/tournaments?page=1&pageSize=5");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PagedResponse<TournamentResponse>>();
        Assert.NotNull(result);
        Assert.True(result.Page >= 1);
        Assert.NotNull(result.Items);
    }

    [Fact]
    public async Task UpdateTournament_WhenExists_Returns200Ok()
    {
        // Arrange: Najpierw tworzymy turniej
        AuthenticateAs(Roles.Organizer);
        var createRequest = new CreateTournamentRequest(
            Name: "Turniej do edycji",
            Discipline: "Szachy",
            Format: TournamentFormat.Cup,
            StartsAt: DateTimeOffset.UtcNow.AddDays(10),
            MaxTeams: 8
        );

        var createResponse = await _client.PostAsJsonAsync("/api/v1/tournaments", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TournamentDetailsResponse>();
        Assert.NotNull(created);

        var updateRequest = new UpdateTournamentRequest(
            Name: "Turniej po edycji",
            Discipline: "Szachy Błyskawiczne",
            Format: TournamentFormat.Cup,
            StartsAt: DateTimeOffset.UtcNow.AddDays(12),
            MaxTeams: 16
        );

        // Act
        var updateResponse = await _client.PutAsJsonAsync($"/api/v1/tournaments/{created.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<TournamentDetailsResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Turniej po edycji", updated.Name);
        Assert.Equal(16, updated.MaxTeams);
    }

    [Fact]
    public async Task DeleteTournament_WhenNotStarted_Returns204NoContent()
    {
        // Arrange
        AuthenticateAs(Roles.Organizer);
        var createRequest = new CreateTournamentRequest(
            Name: "Turniej do usunięcia",
            Discipline: "Fifa",
            Format: TournamentFormat.Cup,
            StartsAt: DateTimeOffset.UtcNow.AddDays(5),
            MaxTeams: 4
        );

        var createResponse = await _client.PostAsJsonAsync("/api/v1/tournaments", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TournamentDetailsResponse>();
        Assert.NotNull(created);

        // Act
        var deleteResponse = await _client.DeleteAsync($"/api/v1/tournaments/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Weryfikacja: pobranie po ID powinno zwrócić 404
        _client.DefaultRequestHeaders.Authorization = null;
        var getResponse = await _client.GetAsync($"/api/v1/tournaments/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }
}
