using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using TurniejePro.Api.Auth;
using TurniejePro.Data;
using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using ContractFormat = TurniejePro.Contracts.Tournaments.TournamentFormat;
using UpdateTournamentRequest = TurniejePro.Contracts.Tournaments.UpdateTournamentRequest;

namespace TurniejePro.Tests;

/// <summary>
/// Edycja turnieju przez API zależnie od statusu (KAN-290): organizator w ograniczonym zakresie, administrator bez
/// ograniczeń statusu.
/// </summary>
public class TournamentEditIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ITokenService _tokenService;
    private readonly Guid _ownerId = Guid.NewGuid();

    public TournamentEditIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        _tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
    }

    [Fact]
    public async Task InProgress_OwnerRenames_Returns200()
    {
        var (id, startsAt) = await SeedAsync(TournamentStatus.InProgress);

        var response = await PutAsync(Owner, id, Request("Nowa nazwa", startsAt, maxTeams: 8));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Nowa nazwa", Load(id).Name);
    }

    [Fact]
    public async Task InProgress_OwnerChangesLimit_Returns400WithExplanation()
    {
        var (id, startsAt) = await SeedAsync(TournamentStatus.InProgress);

        var response = await PutAsync(Owner, id, Request("Turniej", startsAt, maxTeams: 12));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("limit drużyn", problem!.Detail, StringComparison.Ordinal);
        Assert.Equal(8, Load(id).MaxTeams);
    }

    [Fact]
    public async Task InProgress_AdminChangesLimit_Returns200()
    {
        var (id, startsAt) = await SeedAsync(TournamentStatus.InProgress);

        var response = await PutAsync(Admin, id, Request("Turniej", startsAt, maxTeams: 12));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(12, Load(id).MaxTeams);
    }

    [Fact]
    public async Task InProgress_AdminChangesFormatWithSchedule_Returns400()
    {
        var (id, startsAt) = await SeedAsync(TournamentStatus.InProgress);

        var response = await PutAsync(Admin, id, Request("Turniej", startsAt, maxTeams: 8, ContractFormat.Cup));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(TournamentFormat.League, Load(id).Format);
    }

    [Fact]
    public async Task Finished_OwnerRenames_Returns400()
    {
        var (id, startsAt) = await SeedAsync(TournamentStatus.Finished);

        var response = await PutAsync(Owner, id, Request("Nowa nazwa", startsAt, maxTeams: 8));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Turniej", Load(id).Name);
    }

    [Fact]
    public async Task Finished_AdminRenames_Returns200()
    {
        var (id, startsAt) = await SeedAsync(TournamentStatus.Finished);

        var response = await PutAsync(Admin, id, Request("Nowa nazwa", startsAt, maxTeams: 8));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Nowa nazwa", Load(id).Name);
    }

    [Fact]
    public async Task InProgress_WithStartDateInPast_OwnerCanStillRename()
    {
        var (id, startsAt) = await SeedAsync(TournamentStatus.InProgress, startsInDays: -2);

        var response = await PutAsync(Owner, id, Request("Nowa nazwa", startsAt, maxTeams: 8));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // --- Pomocnicze ---

    private string Owner => _tokenService.CreateToken(_ownerId, "owner@example.com", [Roles.User]).AccessToken;

    private string Admin => _tokenService.CreateToken(Guid.NewGuid(), "admin@example.com", [Roles.Admin]).AccessToken;

    private static UpdateTournamentRequest Request(string name, DateTimeOffset startsAt, int maxTeams, ContractFormat format = ContractFormat.League) =>
        new(name, "football", format, startsAt, maxTeams);

    private async Task<HttpResponseMessage> PutAsync(string token, Guid id, UpdateTournamentRequest request)
    {
        using var message = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/tournaments/{id}")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(message);
    }

    private async Task<(Guid Id, DateTimeOffset StartsAt)> SeedAsync(TournamentStatus status, int startsInDays = 14)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();

        var tournament = new Tournament("Turniej", "football", TournamentFormat.League, DateTimeOffset.UtcNow.AddDays(startsInDays), 8, _ownerId);
        var home = new Team("Gospodarze", tournament.Id);
        var away = new Team("Goście", tournament.Id);
        home.Accept();
        away.Accept();
        tournament.Teams.Add(home);
        tournament.Teams.Add(away);
        db.Tournaments.Add(tournament);

        var match = new Match(tournament.Id, 1, 1, home.Id, away.Id);
        db.Matches.Add(match);
        tournament.Start();

        if (status == TournamentStatus.Finished)
        {
            match.SetResult(2, 1);
            tournament.CompleteIfAllMatchesFinished([match]);
        }

        await db.SaveChangesAsync();
        return (tournament.Id, tournament.StartsAt);
    }

    private Tournament Load(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();
        return db.Tournaments.Find(id)!;
    }
}
