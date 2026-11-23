using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualBasic.FileIO;
using TurniejePro.Api.Exports;
using TurniejePro.Contracts.Matches;
using TurniejePro.Contracts.Standings;
using TurniejePro.Contracts.Tournaments;
using TurniejePro.Data;
using TurniejePro.Domain;
using TurniejePro.Domain.Exceptions;
using DomainFormat = TurniejePro.Domain.Enums.TournamentFormat;

namespace TurniejePro.Tests;

public class TournamentCsvExportTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public void Generate_WithGroupsAndMatches_PreservesTextAndOrder()
    {
        var tournamentId = Guid.NewGuid();
        var homeTeam = new MatchTeamResponse(Guid.NewGuid(), "Łódź; \"Orły\"\r\nII");
        var awayTeam = new MatchTeamResponse(Guid.NewGuid(), "  =1+1");
        var played = new MatchResponse(
            Guid.NewGuid(), 1, 1, homeTeam, awayTeam, 3, 1, MatchStatus.Finished, false, null, Group: 2);
        var next = new MatchResponse(
            Guid.NewGuid(), 2, 1, null, homeTeam, null, null, MatchStatus.Scheduled, false, null);
        var schedule = new ScheduleResponse(tournamentId, TournamentFormat.GroupsAndCup,
            [new ScheduleRoundResponse(1, [played]), new ScheduleRoundResponse(2, [next])]);
        var standings = new StandingsResponse(tournamentId,
        [
            new(1, homeTeam.Id, homeTeam.Name, 1, 1, 0, 0, 3, 1, 2, 3, Group: 2),
            new(2, awayTeam.Id, awayTeam.Name, 1, 0, 0, 1, 1, 3, -2, 0, Group: 2)
        ]);

        var bytes = TournamentCsvExport.Generate(new TournamentExportData("@Żółty Turniej", schedule, standings));
        using var reader = new TextFieldParser(new MemoryStream(bytes), Encoding.UTF8) { TrimWhiteSpace = false };
        reader.SetDelimiters(";");

        Assert.True(bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.Equal(["Turniej", "'@Żółty Turniej", "", "", "", "", "", "", "", ""], ReadRow(reader));
        Assert.Equal("Terminarz", ReadRow(reader)[0]);
        Assert.Equal("Gospodarz", ReadRow(reader)[3]);
        Assert.Equal(["1", "1", "B", homeTeam.Name, "3 : 1", "'  =1+1", "Zakończony", "", "", ""], ReadRow(reader));
        Assert.Equal(["2", "1", "", "Do ustalenia", "-", homeTeam.Name, "Zaplanowany", "", "", ""], ReadRow(reader));
        Assert.Equal("Tabela", ReadRow(reader)[0]);
        Assert.Equal("Pkt", ReadRow(reader)[9]);
        Assert.Equal(["B", "1", homeTeam.Name, "1", "1", "0", "0", "3:1", "+2", "3"], ReadRow(reader));
        Assert.Equal(["B", "2", "'  =1+1", "1", "0", "0", "1", "1:3", "-2", "0"], ReadRow(reader));
        Assert.True(reader.EndOfData);
    }

    [Fact]
    public void Generate_WithCupBye_WritesOnlyScheduleAndHidesScore()
    {
        var bye = new MatchResponse(Guid.NewGuid(), 1, 1, new MatchTeamResponse(Guid.NewGuid(), "Orły"), null,
            null, null, MatchStatus.Finished, true, null);
        var schedule = new ScheduleResponse(Guid.NewGuid(), TournamentFormat.Cup, [new ScheduleRoundResponse(1, [bye])]);

        var csv = Encoding.UTF8.GetString(TournamentCsvExport.Generate(new TournamentExportData("Puchar", schedule, null)));

        Assert.Contains("1;1;;Orły;-;Wolny los;Zakończony;;;\r\n", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Tabela", csv, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(DomainFormat.League, true)]
    [InlineData(DomainFormat.Cup, false)]
    public async Task GetAsync_WithoutSchedule_IncludesStandingsOnlyWhenDisplayed(DomainFormat format, bool expectedStandings)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();
        var tournament = new Tournament("Turniej bez terminarza", "football", format, DateTimeOffset.UtcNow.AddDays(3), 8);
        db.Tournaments.Add(tournament);
        var accepted = new Team("Orły", tournament.Id);
        accepted.Accept();
        db.Teams.AddRange(accepted, new Team("W kolejce", tournament.Id));
        await db.SaveChangesAsync();
        var query = scope.ServiceProvider.GetRequiredService<TournamentExportQuery>();

        var data = await query.GetAsync(tournament.Id, CancellationToken.None);

        Assert.Equal(tournament.Name, data.TournamentName);
        Assert.Equal(tournament.Id, data.Schedule.TournamentId);
        Assert.Empty(data.Schedule.Rounds);
        if (expectedStandings)
        {
            Assert.NotNull(data.Standings);
            Assert.Equal(accepted.Id, Assert.Single(data.Standings.Rows).TeamId);
        }
        else
        {
            Assert.Null(data.Standings);
        }
    }

    [Fact]
    public async Task GetAsync_UnknownTournament_ThrowsNotFound()
    {
        using var scope = factory.Services.CreateScope();
        var query = scope.ServiceProvider.GetRequiredService<TournamentExportQuery>();

        await Assert.ThrowsAsync<NotFoundException>(() => query.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }

    private static string[] ReadRow(TextFieldParser reader)
    {
        var row = reader.ReadFields();
        Assert.NotNull(row);
        return row;
    }
}
