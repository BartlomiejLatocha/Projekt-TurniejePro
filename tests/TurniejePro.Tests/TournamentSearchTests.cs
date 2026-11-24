using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TurniejePro.Contracts.Common;
using TurniejePro.Contracts.Tournaments;
using TurniejePro.Data;
using TurniejePro.Domain;
using DomainFormat = TurniejePro.Domain.Enums.TournamentFormat;

namespace TurniejePro.Tests;

/// <summary>
/// Wyszukiwarka turniejów: po nazwie dyscypliny wyświetlanej w UI („siatkówka”, a w bazie „volleyball”),
/// bez wielkości liter i polskich znaków także dla liter spoza alfabetu angielskiego.
/// </summary>
public class TournamentSearchTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly string _tag = Guid.NewGuid().ToString("N")[..6];

    public TournamentSearchTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // --- Nazwa dyscypliny ---

    [Theory]
    [InlineData("siatkówka")]
    [InlineData("Siatkówka")]
    [InlineData("SIATKOWKA")]
    [InlineData("siatk")]
    [InlineData("volley")]
    public async Task SearchByDisciplineName_FindsTournamentStoredWithDisciplineCode(string query)
    {
        var volleyball = await SeedAsync($"Mistrzostwa {_tag}", "volleyball");
        await SeedAsync($"Liga {_tag}", "football");

        var names = await SearchAsync(query);

        Assert.Contains(volleyball, names);
        Assert.DoesNotContain($"Liga {_tag}", names);
    }

    [Theory]
    [InlineData("piłka nożna", "football")]
    [InlineData("pilka", "football")]
    [InlineData("koszyk", "basketball")]
    [InlineData("szachy", "chess")]
    [InlineData("e-sport", "esports")]
    public void CodesMatching_MapsDisplayNameFragmentsToCodes(string query, string expectedCode)
    {
        Assert.Contains(expectedCode, DisciplineCatalog.CodesMatching(query));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hokej")]
    public void CodesMatching_EmptyOrUnknown_ReturnsNothing(string? query)
    {
        Assert.Empty(DisciplineCatalog.CodesMatching(query));
    }

    // --- Nazwa turnieju z polskimi znakami ---

    [Theory]
    [InlineData("żubry")]
    [InlineData("ŻUBRY")]
    [InlineData("zubry")]
    [InlineData("łódź")]
    [InlineData("lodz")]
    [InlineData("  Żubry   Łódź ")]
    public async Task SearchByName_IgnoresCaseAndPolishCharacters(string query)
    {
        var name = await SeedAsync($"Żubry Łódź {_tag}", "football");

        Assert.Contains(name, await SearchAsync(query));
    }

    [Fact]
    public async Task Search_KeepsPagingAndTotalCount()
    {
        for (var i = 1; i <= 5; i++)
        {
            await SeedAsync($"Stronicowanie {_tag} {i}", "chess");
        }

        var page = await _client.GetFromJsonAsync<PagedResponse<TournamentResponse>>(
            $"/api/v1/tournaments?search=stronicowanie%20{_tag}&page=2&pageSize=2");

        Assert.NotNull(page);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
    }

    [Fact]
    public async Task Search_WithoutMatches_ReturnsEmptyPage()
    {
        var page = await _client.GetFromJsonAsync<PagedResponse<TournamentResponse>>(
            $"/api/v1/tournaments?search=nie-ma-takiego-{_tag}");

        Assert.NotNull(page);
        Assert.Equal(0, page.TotalCount);
        Assert.Empty(page.Items);
    }

    // --- Normalizacja ---

    [Theory]
    [InlineData("Łódź", "lodz")]
    [InlineData("  Żubry   Mokotów ", "zubry mokotow")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void Normalize_RemovesCaseDiacriticsAndExtraSpaces(string? text, string expected)
    {
        Assert.Equal(expected, SearchText.Normalize(text));
    }

    [Fact]
    public void Contains_EmptyQueryMatchesEverything()
    {
        Assert.True(SearchText.Contains("Cokolwiek", " "));
        Assert.False(SearchText.Contains(null, "x"));
    }

    // --- Pomocnicze ---

    private async Task<List<string>> SearchAsync(string query)
    {
        var page = await _client.GetFromJsonAsync<PagedResponse<TournamentResponse>>(
            $"/api/v1/tournaments?pageSize=100&search={Uri.EscapeDataString(query)}");

        Assert.NotNull(page);
        return page.Items.Select(t => t.Name).ToList();
    }

    private async Task<string> SeedAsync(string name, string discipline)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();
        db.Tournaments.Add(new Tournament(name, discipline, DomainFormat.League, DateTimeOffset.UtcNow.AddDays(10), 8, null));
        await db.SaveChangesAsync();
        return name;
    }
}
