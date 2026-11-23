using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualBasic.FileIO;
using TurniejePro.Api.Exports;
using TurniejePro.Contracts.Matches;
using TurniejePro.Contracts.Standings;
using TurniejePro.Contracts.Tournaments;
using TurniejePro.Data;
using TurniejePro.Domain;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using DomainFormat = TurniejePro.Domain.Enums.TournamentFormat;

namespace TurniejePro.Tests;

public class TournamentExportParityTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Theory]
    [InlineData(DomainFormat.League)]
    [InlineData(DomainFormat.Cup)]
    [InlineData(DomainFormat.GroupsAndCup)]
    public async Task Export_WithMixedMatchStates_MatchesScheduleAndStandingsViews(DomainFormat format)
    {
        var tournamentId = await SeedTournamentAsync(format);
        using var client = factory.CreateClient();
        var path = $"/api/v1/tournaments/{tournamentId}";
        var tournament = await client.GetFromJsonAsync<TournamentDetailsResponse>(path);
        var schedule = await client.GetFromJsonAsync<ScheduleResponse>($"{path}/schedule");
        Assert.NotNull(tournament);
        Assert.NotNull(schedule);
        var expectedMatches = schedule.Rounds
            .SelectMany(round => round.Matches.Select(match => MatchCells(round.Round, match))).ToArray();
        Assert.Equal(3, expectedMatches.Length);

        var expectedStandings = Array.Empty<string[]>();
        if (format != DomainFormat.Cup)
        {
            var standings = await client.GetFromJsonAsync<StandingsResponse>($"{path}/standings");
            Assert.NotNull(standings);
            Assert.Equal(4, standings.Rows.Count);
            if (format == DomainFormat.GroupsAndCup)
            {
                Assert.Equal([1, 1, 2, 2], standings.Rows.Select(row => row.Group));
                var groupWinner = standings.Rows[0];
                Assert.Equal("Żółwie", groupWinner.TeamName);
                Assert.Equal(5, groupWinner.Points);
                Assert.Equal(1, groupWinner.Played);
            }

            expectedStandings = standings.Rows.Select(StandingsCells).ToArray();
        }

        using var scope = factory.Services.CreateScope();
        var query = scope.ServiceProvider.GetRequiredService<TournamentExportQuery>();
        var exportData = await query.GetAsync(tournamentId, CancellationToken.None);
        var csv = TournamentCsvExport.Generate(exportData);
        var pdf = TournamentPdfExport.Generate(exportData);

        List<string[]> expectedRows =
        [
            ["Turniej", tournament.Name],
            ["Terminarz"],
            ["Runda / kolejka", "Nr", "Grupa", "Gospodarz", "Wynik", "Gość", "Status"]
        ];
        expectedRows.AddRange(expectedMatches);
        if (format != DomainFormat.Cup)
        {
            expectedRows.Add(["Tabela"]);
            expectedRows.Add(["Grupa", "Miejsce", "Drużyna", "M", "W", "R", "P", "Bramki", "Bilans", "Pkt"]);
            expectedRows.AddRange(expectedStandings);
        }

        AssertCsvRows(csv, expectedRows);
        AssertPdfRows(pdf, tournament.Name, expectedRows.Skip(1), format);
    }

    private static void AssertCsvRows(byte[] bytes, IReadOnlyList<string[]> expectedRows)
    {
        using var reader = new TextFieldParser(new MemoryStream(bytes), Encoding.UTF8);
        reader.SetDelimiters(";");
        foreach (var row in expectedRows)
        {
            var parsed = reader.ReadFields();
            Assert.NotNull(parsed);
            Assert.Equal(10, parsed.Length);
            Assert.Equal(row, parsed.Take(row.Length));
            Assert.All(parsed.Skip(row.Length), value => Assert.Empty(value));
        }

        Assert.True(reader.EndOfData);
    }

    private static void AssertPdfRows(byte[] bytes, string tournamentName, IEnumerable<string[]> expectedRows, DomainFormat format)
    {
        using var document = PdfDocument.Open(bytes);
        var text = Compact(string.Concat(document.GetPages().Select(page => ContentOrderTextExtractor.GetText(page))));
        Assert.Contains(Compact(tournamentName), text, StringComparison.Ordinal);
        var start = 0;
        foreach (var row in expectedRows)
        {
            var expected = Compact(string.Concat(row));
            var index = text.IndexOf(expected, start, StringComparison.Ordinal);
            Assert.True(index >= 0, $"Brakuje wiersza PDF lub ma inną kolejność: {string.Join(" | ", row)}");
            start = index + expected.Length;
        }

        if (format == DomainFormat.Cup)
        {
            Assert.DoesNotContain("Tabela", text, StringComparison.Ordinal);
        }
    }

    private async Task<Guid> SeedTournamentAsync(DomainFormat format)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();
        var tournament = new Tournament("Turniej Żółwi", "football", format, DateTimeOffset.UtcNow.AddDays(3), 8,
            scoringRules: new ScoringRules(5, 2, 1));
        var groupStage = format == DomainFormat.GroupsAndCup;
        var turtles = AcceptedTeam("Żółwie", tournament.Id, groupStage ? 1 : null);
        var eagles = AcceptedTeam("Orły", tournament.Id, groupStage ? 1 : null);
        var falcons = AcceptedTeam("Sokoły", tournament.Id, groupStage ? 2 : null);
        var owls = AcceptedTeam("Śląskie Sowy", tournament.Id, groupStage ? 2 : null);

        var first = new Match(tournament.Id, 1, 1, turtles.Id, eagles.Id) { IsGroupStage = groupStage };
        first.SetResult(3, 1);
        var second = new Match(tournament.Id, 1, 2, falcons.Id, format == DomainFormat.Cup ? null : owls.Id)
        {
            IsGroupStage = groupStage
        };
        var final = new Match(tournament.Id, 2, 1, turtles.Id, groupStage ? falcons.Id : null);
        if (format == DomainFormat.Cup)
        {
            second.MarkAsBye();
        }
        else
        {
            second.SetResult(2, 2);
        }

        if (groupStage)
        {
            final.SetResult(4, 2);
        }

        db.Tournaments.Add(tournament);
        db.Teams.AddRange(turtles, eagles, falcons, owls, new Team("Oczekuje na akceptację", tournament.Id));
        db.Matches.AddRange(final, second, first);
        await db.SaveChangesAsync();
        return tournament.Id;
    }

    private static Team AcceptedTeam(string name, Guid tournamentId, int? group)
    {
        var team = new Team(name, tournamentId) { GroupNumber = group };
        team.Accept();
        return team;
    }

    private static string[] MatchCells(int round, MatchResponse match) =>
    [
        Number(round),
        Number(match.Position),
        Group(match.Group),
        match.HomeTeam?.Name ?? "Do ustalenia",
        match is { IsBye: false, HomeScore: { } homeScore, AwayScore: { } awayScore }
            ? FormattableString.Invariant($"{homeScore} : {awayScore}")
            : "-",
        match.IsBye ? "Wolny los" : match.AwayTeam?.Name ?? "Do ustalenia",
        match.Status == MatchStatus.Finished ? "Zakończony" : "Zaplanowany"
    ];

    private static string[] StandingsCells(StandingsRowResponse row) =>
    [
        Group(row.Group),
        Number(row.Place),
        row.TeamName,
        Number(row.Played),
        Number(row.Won),
        Number(row.Drawn),
        Number(row.Lost),
        FormattableString.Invariant($"{row.GoalsFor}:{row.GoalsAgainst}"),
        row.GoalDifference > 0 ? $"+{Number(row.GoalDifference)}" : Number(row.GoalDifference),
        Number(row.Points)
    ];

    private static string Group(int? group) => group is { } number ? ((char)('A' + number - 1)).ToString() : "";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Compact(string text) => string.Concat(text.Where(character => !char.IsWhiteSpace(character)));
}
