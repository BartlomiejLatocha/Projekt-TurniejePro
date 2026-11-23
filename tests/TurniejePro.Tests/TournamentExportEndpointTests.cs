using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TurniejePro.Api.Exports;
using TurniejePro.Data;
using TurniejePro.Domain;
using TurniejePro.Domain.Enums;

namespace TurniejePro.Tests;

/// <summary>
/// Pobieranie raportu turnieju jako pliku (KAN-311): CSV i PDF, publicznie, z nazwą pliku z nazwy turnieju i daty.
/// </summary>
public class TournamentExportEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly DateTimeOffset Today = new(2026, 11, 23, 15, 30, 0, TimeSpan.Zero);

    // Jedna aplikacja z podstawionym zegarem: przez nią zasiewamy dane i wysyłamy żądania
    private readonly WebApplicationFactory<Program> _app;
    private readonly HttpClient _client;

    public TournamentExportEndpointTests(CustomWebApplicationFactory factory)
    {
        _app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(Today))));
        _client = _app.CreateClient();
    }

    [Fact]
    public async Task Csv_WithoutToken_ReturnsFileWithScheduleAndStandings()
    {
        var id = await SeedLeagueAsync("Liga Osiedlowa Bemowo");

        using var response = await _client.GetAsync($"/api/v1/tournaments/{id}/export/csv");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("liga-osiedlowa-bemowo-2026-11-23.csv", response.Content.Headers.ContentDisposition?.FileNameStar);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(Encoding.UTF8.Preamble.ToArray(), bytes[..3]);
        var text = Encoding.UTF8.GetString(bytes[3..]);
        Assert.Contains("Liga Osiedlowa Bemowo", text, StringComparison.Ordinal);
        Assert.Contains("Terminarz", text, StringComparison.Ordinal);
        Assert.Contains("Tabela", text, StringComparison.Ordinal);
        Assert.Contains("Orły", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pdf_WithoutToken_ReturnsPdfFile()
    {
        var id = await SeedLeagueAsync("Puchar Ziemi Łódzkiej");

        using var response = await _client.GetAsync($"/api/v1/tournaments/{id}/export/pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("puchar-ziemi-lodzkiej-2026-11-23.pdf", response.Content.Headers.ContentDisposition?.FileNameStar);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Theory]
    [InlineData("CSV", "text/csv")]
    [InlineData("Pdf", "application/pdf")]
    public async Task Format_IsCaseInsensitive(string format, string expectedMediaType)
    {
        var id = await SeedLeagueAsync("Turniej");

        using var response = await _client.GetAsync($"/api/v1/tournaments/{id}/export/{format}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedMediaType, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UnknownFormat_Returns400()
    {
        var id = await SeedLeagueAsync("Turniej");

        using var response = await _client.GetAsync($"/api/v1/tournaments/{id}/export/xlsx");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Nieobsługiwany format", problem!.Title);
        Assert.Contains("csv, pdf", problem.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("pdf")]
    public async Task UnknownTournament_Returns404(string format)
    {
        using var response = await _client.GetAsync($"/api/v1/tournaments/{Guid.NewGuid()}/export/{format}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CupWithoutSchedule_ReturnsFileWithoutStandings()
    {
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();
            var cup = new Tournament("Puchar bez terminarza", "football", TournamentFormat.Cup, Today.AddDays(30), 8, null);
            db.Tournaments.Add(cup);
            await db.SaveChangesAsync();

            using var response = await _client.GetAsync($"/api/v1/tournaments/{cup.Id}/export/csv");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var text = await response.Content.ReadAsStringAsync();
            Assert.Contains("Terminarz", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Tabela", text, StringComparison.Ordinal);
        }
    }

    // --- Nazwa pliku ---

    [Theory]
    [InlineData("Liga Osiedlowa Bemowo – wiosna 2026", "liga-osiedlowa-bemowo-wiosna-2026-2026-11-23.csv")]
    [InlineData("Łódzka Liga Żaków: edycja 2/3", "lodzka-liga-zakow-edycja-2-3-2026-11-23.csv")]
    [InlineData("  ***  ", "turniej-2026-11-23.csv")]
    [InlineData("", "turniej-2026-11-23.csv")]
    public void FileName_IsSafeAsciiSlugWithDate(string tournamentName, string expected)
    {
        Assert.Equal(expected, ExportFileName.For(tournamentName, new DateOnly(2026, 11, 23), "csv"));
    }

    [Fact]
    public void FileName_LongNameIsShortened()
    {
        var fileName = ExportFileName.For(new string('a', 200), new DateOnly(2026, 11, 23), "pdf");

        Assert.Equal(new string('a', 60) + "-2026-11-23.pdf", fileName);
    }

    [Fact]
    public void FileName_InvalidArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => ExportFileName.For(null!, new DateOnly(2026, 11, 23), "csv"));
        Assert.Throws<ArgumentException>(() => ExportFileName.For("Turniej", new DateOnly(2026, 11, 23), " "));
    }

    // --- Pomocnicze ---

    private async Task<Guid> SeedLeagueAsync(string name)
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();

        var tournament = new Tournament(name, "football", TournamentFormat.League, Today.AddDays(30), 8, null);
        var home = new Team("Orły", tournament.Id);
        var away = new Team("Sokoły", tournament.Id);
        home.Accept();
        away.Accept();
        tournament.Teams.Add(home);
        tournament.Teams.Add(away);
        db.Tournaments.Add(tournament);

        var match = new Match(tournament.Id, 1, 1, home.Id, away.Id);
        db.Matches.Add(match);
        tournament.Start();
        match.SetResult(2, 1);

        await db.SaveChangesAsync();
        return tournament.Id;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
