using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TurniejePro.Api.Auth;
using TurniejePro.Contracts.Tournaments;
using static System.Net.HttpStatusCode;

namespace TurniejePro.Tests;

/// <summary>
/// Pełna macierz „endpoint × rola × oczekiwany kod” (KAN-67), oparta na tabeli uprawnień z docs/authorization.md.
/// Zmiana macierzy w dokumencie oznacza zmianę wierszy w <see cref="Matrix"/>.
/// Nowy chroniony endpoint = jeden nowy wiersz (wywołanie <c>AddEndpoint</c>).
/// Endpointów, które jeszcze nie istnieją (zgłoszenia drużyn, wyniki, terminarz), pilnują polityki
/// wystawione przez <c>TestAuthorizationController</c>.
/// </summary>
public class AuthorizationMatrixTests : IClassFixture<CustomWebApplicationFactory>
{
    public const string CreateTournament = "/api/v1/tournaments";
    public const string CreateTournamentPolicyEndpoint = "/api/test-auth/create-tournament";
    public const string RegisterTeamPolicyEndpoint = "/api/test-auth/register-team";
    public const string NoAttributes = "/api/test-auth/no-attributes";

    private readonly HttpClient _client;
    private readonly TestTokens _tokens;

    public AuthorizationMatrixTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _tokens = new TestTokens(factory.Services);
    }

    /// <summary>Kolumny: metoda HTTP, ścieżka, rola w tokenie (null = brak tokenu), oczekiwany kod.</summary>
    public static TheoryData<string, string, string?, HttpStatusCode> Matrix
    {
        get
        {
            var data = new TheoryData<string, string, string?, HttpStatusCode>();

            // Endpointy publiczne ([AllowAnonymous])
            AddEndpoint(data, "GET", "/api/v1/health", anonymous: OK, user: OK, admin: OK);
            AddEndpoint(data, "GET", "/api/v1/tournaments", anonymous: OK, user: OK, admin: OK);
            AddEndpoint(data, "GET", $"/api/v1/tournaments/{Guid.Empty}", anonymous: OK, user: OK, admin: OK);

            // Wymagane samo zalogowanie ([Authorize] bez polityki)
            AddEndpoint(data, "GET", "/api/v1/auth/me", anonymous: Unauthorized, user: OK, admin: OK);

            AddEndpoint(data, "POST", CreateTournament, anonymous: Unauthorized, user: Created, admin: Created);
            AddEndpoint(data, "GET", CreateTournamentPolicyEndpoint, anonymous: Unauthorized, user: OK, admin: OK);
            AddEndpoint(data, "GET", RegisterTeamPolicyEndpoint, anonymous: Unauthorized, user: OK, admin: OK);

            // Brak atrybutów: działa polityka domyślna (wymagane logowanie), więc zapomniany atrybut nie otwiera luki
            AddEndpoint(data, "GET", NoAttributes, anonymous: Unauthorized, user: OK, admin: OK);

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task Endpoint_ReturnsExpectedStatusCode(string method, string path, string? role, HttpStatusCode expected)
    {
        // Arrange
        var token = role is null ? null : _tokens.ForRoles(role);

        // Act
        var status = await GetStatusAsync(method, path, token);

        // Assert
        Assert.Equal(expected, status);
    }

    // Poniższe testy sprawdzają samo uwierzytelnianie. Token ma rolę User, żeby było widać, że 401
    // wynika z wadliwego tokenu, a nie z braku uprawnień.

    [Fact]
    public async Task MalformedToken_Returns401()
    {
        var status = await GetStatusAsync("GET", CreateTournamentPolicyEndpoint, "to.nie.jest.token");

        Assert.Equal(Unauthorized, status);
    }

    [Fact]
    public async Task ExpiredToken_Returns401()
    {
        var status = await GetStatusAsync("GET", CreateTournamentPolicyEndpoint, _tokens.Expired(Roles.User));

        Assert.Equal(Unauthorized, status);
    }

    [Fact]
    public async Task TokenSignedWithWrongKey_Returns401()
    {
        var status = await GetStatusAsync("GET", CreateTournamentPolicyEndpoint, _tokens.SignedWithWrongKey(Roles.User));

        Assert.Equal(Unauthorized, status);
    }

    [Fact]
    public async Task TokenWithWrongIssuer_Returns401()
    {
        var status = await GetStatusAsync("GET", CreateTournamentPolicyEndpoint, _tokens.WithWrongIssuer(Roles.User));

        Assert.Equal(Unauthorized, status);
    }

    [Fact]
    public async Task TokenWithWrongAudience_Returns401()
    {
        var status = await GetStatusAsync("GET", CreateTournamentPolicyEndpoint, _tokens.WithWrongAudience(Roles.User));

        Assert.Equal(Unauthorized, status);
    }

    [Fact]
    public async Task ValidTokenWithoutRoles_OnCreateTournamentEndpoint_Returns200()
    {
        var status = await GetStatusAsync("GET", CreateTournamentPolicyEndpoint, _tokens.ForRoles());

        Assert.Equal(OK, status);
    }

    [Fact]
    public async Task ValidTokenWithoutRoles_OnEndpointWithoutAttributes_Returns200()
    {
        var status = await GetStatusAsync("GET", NoAttributes, _tokens.ForRoles());

        Assert.Equal(OK, status);
    }

    [Fact]
    public async Task UserWithSeveralRoles_Returns200()
    {
        var status = await GetStatusAsync("GET", CreateTournamentPolicyEndpoint, _tokens.ForRoles(Roles.User, Roles.Admin));

        Assert.Equal(OK, status);
    }

    private static void AddEndpoint(
        TheoryData<string, string, string?, HttpStatusCode> data,
        string method,
        string path,
        HttpStatusCode anonymous,
        HttpStatusCode user,
        HttpStatusCode admin)
    {
        data.Add(method, path, null, anonymous);
        data.Add(method, path, Roles.User, user);
        data.Add(method, path, Roles.Admin, admin);
    }

    private static CreateTournamentRequest CreateValidTournament() =>
        new("Turniej testowy", "Piłka nożna", TournamentFormat.League, DateTimeOffset.UtcNow.AddDays(7), MaxTeams: 8);

    private async Task<HttpStatusCode> GetStatusAsync(string method, string path, string? token)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // Poprawne body, żeby o wyniku decydowała autoryzacja, a nie walidacja modelu
        if (request.Method == HttpMethod.Post)
        {
            request.Content = JsonContent.Create(CreateValidTournament());
        }

        using var response = await _client.SendAsync(request);
        return response.StatusCode;
    }
}
