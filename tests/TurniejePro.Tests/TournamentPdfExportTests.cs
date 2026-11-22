using TurniejePro.Api.Exports;
using TurniejePro.Contracts.Matches;
using TurniejePro.Contracts.Standings;
using TurniejePro.Contracts.Tournaments;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace TurniejePro.Tests;

public class TournamentPdfExportTests
{
    [Fact]
    public void Generate_MultiPageSchedule_RepeatsHeadersAndPreservesPolishText()
    {
        var tournamentId = Guid.NewGuid();
        var home = new MatchTeamResponse(Guid.NewGuid(), "Żółwie z Łodzi");
        var away = new MatchTeamResponse(Guid.NewGuid(), "Śląska Drużyna");
        var matches = Enumerable.Range(1, 65)
            .Select(position => new MatchResponse(Guid.NewGuid(), 1, position, home, away, 2, 1,
                MatchStatus.Finished, false, null))
            .ToArray();
        var schedule = new ScheduleResponse(tournamentId, TournamentFormat.League, [new ScheduleRoundResponse(1, matches)]);
        var standings = new StandingsResponse(tournamentId, [new(1, home.Id, home.Name, 65, 65, 0, 0, 130, 65, 65, 195)]);

        var pdf = TournamentPdfExport.Generate(new TournamentExportData("Puchar Łąk", schedule, standings));

        using var document = PdfDocument.Open(pdf);
        var pageTexts = document.GetPages().Select(page => ContentOrderTextExtractor.GetText(page)).ToArray();
        Assert.True(pageTexts.Count(page => page.Contains("Gospodarz", StringComparison.Ordinal)) >= 2);
        Assert.All(pageTexts, pageText => Assert.Contains("Puchar Łąk", pageText, StringComparison.Ordinal));
        var text = string.Join(' ', pageTexts);
        Assert.Contains("Żółwie z Łodzi", text, StringComparison.Ordinal);
        Assert.Contains("Śląska Drużyna", text, StringComparison.Ordinal);
        Assert.Contains("2 : 1", text, StringComparison.Ordinal);
        Assert.Contains("130:65", text, StringComparison.Ordinal);
        Assert.Contains("195", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TournamentFormat.Cup)]
    [InlineData(TournamentFormat.League)]
    public void Generate_WithoutSchedule_ReflectsAvailableSections(TournamentFormat format)
    {
        var id = Guid.NewGuid();
        var standings = format == TournamentFormat.Cup ? null : new StandingsResponse(id, []);
        var data = new TournamentExportData("Turniej", new ScheduleResponse(id, format, []), standings);

        using var document = PdfDocument.Open(TournamentPdfExport.Generate(data));
        var text = ContentOrderTextExtractor.GetText(document.GetPage(1));

        Assert.Contains("Brak wygenerowanego terminarza.", text, StringComparison.Ordinal);
        if (format == TournamentFormat.Cup)
        {
            Assert.DoesNotContain("Tabela", text, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("Brak zaakceptowanych drużyn w tabeli.", text, StringComparison.Ordinal);
        }
    }
}
