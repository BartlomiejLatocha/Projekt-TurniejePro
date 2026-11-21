using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TurniejePro.Api.Auth;
using TurniejePro.Contracts.Tournaments;
using Xunit;

namespace TurniejePro.Tests;

public class TournamentStatusIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ITokenService _tokenService;

    public TournamentStatusIntegrationTests(CustomWebApplicationFactory factory)
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

    private async Task<TournamentDetailsResponse> CreateCupAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/tournaments", new CreateTournamentRequest(
            Name: "Status test",
            Discipline: "chess",
            Format: TournamentFormat.Cup,
            StartsAt: DateTimeOffset.UtcNow.AddDays(10),
            MaxTeams: 8));

        var created = await response.Content.ReadFromJsonAsync<TournamentDetailsResponse>();
        Assert.NotNull(created);
        return created;
    }

    [Fact]
    public async Task NewTournament_HasDraftStatus()
    {
        AuthenticateAs(Roles.User);

        var created = await CreateCupAsync();

        Assert.Equal(TournamentStatus.Draft, created.Status);
        Assert.False(created.IsStarted);
    }

    [Fact]
    public async Task OpenRegistration_MovesDraftToRegistrationOpen()
    {
        AuthenticateAs(Roles.User);
        var created = await CreateCupAsync();

        var opened = await _client.PatchAsync($"/api/v1/tournaments/{created.Id}/open-registration", null);
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        var afterOpen = await opened.Content.ReadFromJsonAsync<TournamentDetailsResponse>();
        Assert.Equal(TournamentStatus.RegistrationOpen, afterOpen!.Status);
    }

    // KAN-290: turniej rozpoczyna wygenerowanie terminarza. Ręczny start bez terminarza jest odrzucany, a turniej zostaje
    // w zapisach. Start po terminarzu, zmianę formatu po starcie i pozostałe przejścia sprawdza TournamentLifecycleIntegrationTests.
    [Fact]
    public async Task Start_WithoutSchedule_Returns400AndKeepsRegistrationOpen()
    {
        AuthenticateAs(Roles.User);
        var created = await CreateCupAsync();
        await _client.PatchAsync($"/api/v1/tournaments/{created.Id}/open-registration", null);

        var started = await _client.PatchAsync($"/api/v1/tournaments/{created.Id}/start", null);

        Assert.Equal(HttpStatusCode.BadRequest, started.StatusCode);
        var tournament = await _client.GetFromJsonAsync<TournamentDetailsResponse>($"/api/v1/tournaments/{created.Id}");
        Assert.Equal(TournamentStatus.RegistrationOpen, tournament!.Status);
        Assert.False(tournament.IsStarted);
    }

    [Fact]
    public async Task Start_ByAnotherUser_Returns403()
    {
        AuthenticateAs(Roles.User);
        var created = await CreateCupAsync();

        AuthenticateAs(Roles.User);
        var response = await _client.PatchAsync($"/api/v1/tournaments/{created.Id}/start", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
