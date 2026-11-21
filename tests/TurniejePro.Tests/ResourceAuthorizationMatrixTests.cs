using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TurniejePro.Api.Auth;
using TurniejePro.Contracts.Matches;
using TurniejePro.Data;
using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using ContractsTournaments = TurniejePro.Contracts.Tournaments;

namespace TurniejePro.Tests;

/// <summary>
/// Macierz „endpoint × wywołujący × kod” dla endpointów zasobowych (KAN-286), czyli takich, które zmieniają cudzy
/// turniej albo cudzą drużynę. Brak tokenu daje 401, zalogowany użytkownik, który nie jest organizatorem tego
/// turnieju (także kapitan drużyny i organizator innego turnieju), dostaje 403 i nic się nie zmienia, a organizator
/// turnieju oraz administrator dostają kod sukcesu. Nieistniejący zasób: bez tokenu 401, po zalogowaniu 404.
/// </summary>
public class ResourceAuthorizationMatrixTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly Dictionary<string, EndpointCase> Endpoints = new()
    {
        ["PUT /tournaments/{id}"] = new(
            HttpMethod.Put,
            ids => $"/api/v1/tournaments/{ids.TournamentId}",
            () => new ContractsTournaments.UpdateTournamentRequest(
                "Nazwa po zmianie",
                "Siatkówka",
                ContractsTournaments.TournamentFormat.League,
                DateTimeOffset.UtcNow.AddDays(14),
                8),
            HttpStatusCode.OK,
            NeedsMatch: false,
            IsUntouched: (db, ids) => db.Tournaments.Single(t => t.Id == ids.TournamentId).Name == ids.TournamentName),

        ["DELETE /tournaments/{id}"] = new(
            HttpMethod.Delete,
            ids => $"/api/v1/tournaments/{ids.TournamentId}",
            () => null,
            HttpStatusCode.NoContent,
            NeedsMatch: false,
            IsUntouched: (db, ids) => db.Tournaments.Any(t => t.Id == ids.TournamentId)),

        ["POST /tournaments/{id}/schedule"] = new(
            HttpMethod.Post,
            ids => $"/api/v1/tournaments/{ids.TournamentId}/schedule",
            () => null,
            HttpStatusCode.Created,
            NeedsMatch: false,
            IsUntouched: (db, ids) => !db.Matches.Any(m => m.TournamentId == ids.TournamentId)),

        ["DELETE /tournaments/{id}/schedule"] = new(
            HttpMethod.Delete,
            ids => $"/api/v1/tournaments/{ids.TournamentId}/schedule",
            () => null,
            HttpStatusCode.NoContent,
            NeedsMatch: true,
            IsUntouched: (db, ids) => db.Matches.Any(m => m.TournamentId == ids.TournamentId)),

        ["PATCH /teams/{id}/accept"] = new(
            HttpMethod.Patch,
            ids => $"/api/v1/teams/{ids.PendingTeamId}/accept",
            () => null,
            HttpStatusCode.OK,
            NeedsMatch: false,
            IsUntouched: (db, ids) => db.Teams.Single(t => t.Id == ids.PendingTeamId).Status == TeamRegistrationStatus.Pending),

        ["PATCH /teams/{id}/reject"] = new(
            HttpMethod.Patch,
            ids => $"/api/v1/teams/{ids.PendingTeamId}/reject",
            () => null,
            HttpStatusCode.OK,
            NeedsMatch: false,
            IsUntouched: (db, ids) => db.Teams.Single(t => t.Id == ids.PendingTeamId).Status == TeamRegistrationStatus.Pending),

        ["PUT /tournaments/{id}/matches/{id}/result"] = new(
            HttpMethod.Put,
            ids => $"/api/v1/tournaments/{ids.TournamentId}/matches/{ids.MatchId}/result",
            () => new UpdateMatchResultRequest(2, 1),
            HttpStatusCode.OK,
            NeedsMatch: true,
            IsUntouched: (db, ids) => db.Matches.Single(m => m.Id == ids.MatchId).HomeScore is null)
    };

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ResourceAuthorizationMatrixTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>Kto wywołuje endpoint.</summary>
    public enum Caller
    {
        /// <summary>Brak tokenu.</summary>
        Anonymous,

        /// <summary>Zalogowany użytkownik niezwiązany z turniejem.</summary>
        Stranger,

        /// <summary>Organizator innego turnieju.</summary>
        OtherOrganizer,

        /// <summary>Kapitan drużyny z tego turnieju (także tej, której dotyczy żądanie).</summary>
        TeamCaptain,

        /// <summary>Organizator tego turnieju.</summary>
        Owner,

        /// <summary>Administrator.</summary>
        Admin
    }

    public static TheoryData<string, Caller> Matrix
    {
        get
        {
            var data = new TheoryData<string, Caller>();

            foreach (var endpoint in Endpoints.Keys)
            {
                foreach (var caller in Enum.GetValues<Caller>())
                {
                    data.Add(endpoint, caller);
                }
            }

            return data;
        }
    }

    public static TheoryData<string> EndpointNames
    {
        get
        {
            var data = new TheoryData<string>();

            foreach (var endpoint in Endpoints.Keys)
            {
                data.Add(endpoint);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task Endpoint_ReturnsExpectedStatusForCaller_AndRefusalChangesNothing(string endpointName, Caller caller)
    {
        var endpoint = Endpoints[endpointName];
        var ids = await SeedAsync(endpoint.NeedsMatch);
        var token = await TokenForAsync(caller, ids);

        var response = await SendAsync(endpoint, ids, token);

        Assert.Equal(ExpectedStatus(caller, endpoint), response.StatusCode);

        if (caller is not (Caller.Owner or Caller.Admin))
        {
            Assert.True(Read(db => endpoint.IsUntouched(db, ids)), "Odmowa nie powinna zmienić danych.");
        }
    }

    [Theory]
    [MemberData(nameof(EndpointNames))]
    public async Task NonexistentResource_WithoutToken_Returns401BeforeLookingItUp(string endpointName)
    {
        var response = await SendAsync(Endpoints[endpointName], RandomIds(), token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(EndpointNames))]
    public async Task NonexistentResource_WithToken_Returns404(string endpointName)
    {
        var token = CreateToken(Guid.NewGuid(), Roles.User);

        var response = await SendAsync(Endpoints[endpointName], RandomIds(), token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static HttpStatusCode ExpectedStatus(Caller caller, EndpointCase endpoint) => caller switch
    {
        Caller.Anonymous => HttpStatusCode.Unauthorized,
        Caller.Owner or Caller.Admin => endpoint.Success,
        _ => HttpStatusCode.Forbidden
    };

    private static ResourceIds RandomIds() =>
        new(Guid.NewGuid(), "Nieistniejący", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    private async Task<string?> TokenForAsync(Caller caller, ResourceIds ids)
    {
        switch (caller)
        {
            case Caller.Anonymous:
                return null;
            case Caller.Stranger:
                return CreateToken(Guid.NewGuid(), Roles.User);
            case Caller.OtherOrganizer:
                var other = await SeedAsync(withMatch: false);
                return CreateToken(other.OwnerId, Roles.User);
            case Caller.TeamCaptain:
                return CreateToken(ids.CaptainId, Roles.User);
            case Caller.Owner:
                return CreateToken(ids.OwnerId, Roles.User);
            case Caller.Admin:
                return CreateToken(Guid.NewGuid(), Roles.Admin);
            default:
                throw new ArgumentOutOfRangeException(nameof(caller), caller, "Nieznany wywołujący.");
        }
    }

    private string CreateToken(Guid userId, string role)
    {
        using var scope = _factory.Services.CreateScope();

        return scope.ServiceProvider.GetRequiredService<ITokenService>()
            .CreateToken(userId, $"{userId}@example.com", [role]).AccessToken;
    }

    private async Task<HttpResponseMessage> SendAsync(EndpointCase endpoint, ResourceIds ids, string? token)
    {
        using var request = new HttpRequestMessage(endpoint.Method, endpoint.Url(ids));

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (endpoint.Body() is { } body)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        return await _client.SendAsync(request);
    }

    /// <summary>
    /// Turniej ligowy z organizatorem, trzema zaakceptowanymi drużynami (kapitanem pierwszej jest <c>CaptainId</c>)
    /// i jedną oczekującą drużyną tego samego kapitana. Opcjonalnie z jednym meczem pierwszej z drużyn z drugą.
    /// </summary>
    private async Task<ResourceIds> SeedAsync(bool withMatch)
    {
        var ownerId = Guid.NewGuid();
        var captainId = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();

        var tournament = new Tournament($"Turniej {Guid.NewGuid():N}", "Piłka nożna", TournamentFormat.League, DateTimeOffset.UtcNow.AddDays(7), 8, ownerId);
        var accepted = Enumerable.Range(1, 3)
            .Select(number =>
            {
                var team = new Team($"Drużyna {number}", tournament.Id, number == 1 ? captainId : null);
                team.Accept();

                return team;
            })
            .ToList();
        var pending = new Team("Oczekująca", tournament.Id, captainId);

        foreach (var team in accepted.Append(pending))
        {
            tournament.Teams.Add(team);
        }

        db.Tournaments.Add(tournament);

        var match = new Match(tournament.Id, 1, 1, accepted[0].Id, accepted[1].Id);
        if (withMatch)
        {
            // Turniej z terminarzem jest rozpoczęty (KAN-290), a wyniki można wpisywać tylko w rozpoczętym turnieju
            db.Matches.Add(match);
            tournament.Start();
        }

        await db.SaveChangesAsync();

        return new ResourceIds(tournament.Id, tournament.Name, pending.Id, match.Id, ownerId, captainId);
    }

    private T Read<T>(Func<TurniejeProDbContext, T> query)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();

        return query(db);
    }

    private sealed record ResourceIds(
        Guid TournamentId,
        string TournamentName,
        Guid PendingTeamId,
        Guid MatchId,
        Guid OwnerId,
        Guid CaptainId);

    private sealed record EndpointCase(
        HttpMethod Method,
        Func<ResourceIds, string> Url,
        Func<object?> Body,
        HttpStatusCode Success,
        bool NeedsMatch,
        Func<TurniejeProDbContext, ResourceIds, bool> IsUntouched);
}
