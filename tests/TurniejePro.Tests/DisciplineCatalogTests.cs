using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TurniejePro.Contracts.Tournaments;
using Xunit;

namespace TurniejePro.Tests;

public class DisciplineCatalogTests
{
    [Fact]
    public void Find_KnownCode_ReturnsDefinition()
    {
        var definition = DisciplineCatalog.Find("football");

        Assert.NotNull(definition);
        Assert.Equal("Piłka nożna", definition!.Name);
        Assert.Equal(3, definition.PointsForWin);
        Assert.True(definition.AllowDraws);
    }

    [Theory]
    [InlineData("Pilka nozna")]
    [InlineData("PIŁKA NOŻNA")]
    [InlineData("football")]
    public void Find_LegacyNameOrCode_MatchesIgnoringCaseAndDiacritics(string value)
    {
        Assert.Equal("football", DisciplineCatalog.Find(value)?.Code);
    }

    [Fact]
    public void Find_UnknownText_ReturnsNull()
    {
        Assert.Null(DisciplineCatalog.Find("Dowolny tekst"));
    }

    [Fact]
    public void NameOf_LegacyValue_ReturnsCatalogName()
    {
        Assert.Equal("Piłka nożna", DisciplineCatalog.NameOf("Pilka nozna"));
    }

    [Fact]
    public void NameOf_UnknownValue_ReturnsValueUnchanged()
    {
        Assert.Equal("Dowolny tekst", DisciplineCatalog.NameOf("Dowolny tekst"));
    }
}

public class DisciplineCreateIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestTokens _testTokens;

    public DisciplineCreateIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _testTokens = new TestTokens(factory.Services);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", _testTokens.ForRoles("Organizer"));
    }

    [Fact]
    public async Task POST_Tournament_UnknownDiscipline_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/tournaments", new
        {
            name = $"Dyscyplina {Guid.NewGuid():N}",
            discipline = "Dowolny tekst",
            format = "League",
            startsAt = DateTimeOffset.UtcNow.AddDays(10),
            maxTeams = 4
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_Tournament_WithCustomScoringRules_SavesThem()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/tournaments", new
        {
            name = $"Punkty {Guid.NewGuid():N}",
            discipline = "basketball",
            format = "League",
            startsAt = DateTimeOffset.UtcNow.AddDays(10),
            maxTeams = 4,
            scoringRules = new { pointsForWin = 2, pointsForDraw = 0, pointsForLoss = 1, allowDraws = false }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<TournamentIdOnly>();
        var standings = await _client.GetFromJsonAsync<Contracts.Standings.StandingsResponse>(
            $"/api/v1/tournaments/{created!.Id}/standings");
        Assert.NotNull(standings);
    }

    private sealed record TournamentIdOnly(Guid Id);
}
