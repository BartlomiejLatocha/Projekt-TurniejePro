using TurniejePro.Domain;
using TurniejePro.Domain.Standings;

namespace TurniejePro.Tests;

public class StandingsCalculatorTests
{
    private static readonly Guid TournamentId = Guid.NewGuid();

    [Fact]
    public void Calculate_NullTeams_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => StandingsCalculator.Calculate(null!, []));
    }

    [Fact]
    public void Calculate_NullMatches_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => StandingsCalculator.Calculate([Guid.NewGuid()], null!));
    }

    [Fact]
    public void Calculate_DuplicatedTeam_ThrowsArgumentException()
    {
        var team = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => StandingsCalculator.Calculate([team, team], []));
    }

    [Fact]
    public void Calculate_NoMatches_AllTeamsShareFirstPlaceInInputOrder()
    {
        var (teams, labels) = CreateTeams("A", "B", "C");

        var rows = StandingsCalculator.Calculate(teams, []);

        Assert.Equal(["1:A", "1:B", "1:C"], Describe(rows, labels));
        Assert.All(rows, row => Assert.Equal(0, row.Points));
    }

    [Fact]
    public void Calculate_Win_GivesThreePointsToWinnerAndNoneToLoser()
    {
        var (teams, _) = CreateTeams("A", "B");

        var rows = StandingsCalculator.Calculate(teams, [Finished(teams[0], teams[1], 2, 1)]);

        var winner = rows[0];
        Assert.Equal(teams[0], winner.TeamId);
        Assert.Equal((1, 1, 0, 0, 2, 1, 1, 3), (winner.Played, winner.Won, winner.Drawn, winner.Lost, winner.GoalsFor, winner.GoalsAgainst, winner.GoalDifference, winner.Points));

        var loser = rows[1];
        Assert.Equal(teams[1], loser.TeamId);
        Assert.Equal((1, 0, 0, 1, 1, 2, -1, 0), (loser.Played, loser.Won, loser.Drawn, loser.Lost, loser.GoalsFor, loser.GoalsAgainst, loser.GoalDifference, loser.Points));
    }

    [Fact]
    public void Calculate_Draw_GivesOnePointToBothTeams()
    {
        var (teams, _) = CreateTeams("A", "B");

        var rows = StandingsCalculator.Calculate(teams, [Finished(teams[0], teams[1], 1, 1)]);

        Assert.All(rows, row =>
        {
            Assert.Equal(1, row.Drawn);
            Assert.Equal(1, row.Points);
            Assert.Equal(0, row.GoalDifference);
        });
    }

    [Fact]
    public void Calculate_FullLeague_OrdersByPointsThenGoalDifference()
    {
        var (teams, labels) = CreateTeams("A", "B", "C", "D");
        var (a, b, c, d) = (teams[0], teams[1], teams[2], teams[3]);

        var rows = StandingsCalculator.Calculate(
            teams,
            [
                Finished(a, b, 2, 0),
                Finished(a, c, 1, 1),
                Finished(a, d, 3, 0),
                Finished(b, c, 0, 1),
                Finished(b, d, 2, 2),
                Finished(c, d, 1, 0)
            ]);

        // A i C mają po 7 punktów (A lepszy bilansem +5 do +2), B i D po 1 punkt (B lepszy bilansem -3 do -4)
        Assert.Equal(["1:A", "2:C", "3:B", "4:D"], Describe(rows, labels));
        Assert.Equal([7, 7, 1, 1], rows.Select(row => row.Points));
        Assert.Equal([5, 2, -3, -4], rows.Select(row => row.GoalDifference));
    }

    [Fact]
    public void Calculate_EqualPointsAndGoalDifference_MoreGoalsScoredWins()
    {
        var (teams, labels) = CreateTeams("C", "B", "A");
        var (c, b, a) = (teams[0], teams[1], teams[2]);

        var rows = StandingsCalculator.Calculate(teams, [Finished(a, c, 3, 2), Finished(b, c, 1, 0)]);

        // A i B mają po 3 punkty i bilans +1, ale A strzeliła więcej bramek
        Assert.Equal(["1:A", "2:B", "3:C"], Describe(rows, labels));
    }

    [Fact]
    public void Calculate_EqualPointsGoalDifferenceAndGoals_HeadToHeadDecides()
    {
        // B jest przed A na liście wejściowej, więc o kolejności decyduje wyłącznie wygrana A z B
        var (teams, labels) = CreateTeams("B", "A", "X", "Y");
        var (b, a, x, y) = (teams[0], teams[1], teams[2], teams[3]);

        var rows = StandingsCalculator.Calculate(
            teams,
            [
                Finished(a, b, 1, 0),
                Finished(a, x, 0, 1),
                Finished(b, y, 1, 0)
            ]);

        // X ma 3 punkty i bilans +1. A i B mają po 3 punkty, bilans 0 i 1 bramkę, więc rozstrzyga mecz bezpośredni
        Assert.Equal(["1:X", "2:A", "3:B", "4:Y"], Describe(rows, labels));
    }

    [Fact]
    public void Calculate_FullyEqualTeams_ShareThePlaceAndKeepInputOrder()
    {
        var (teams, labels) = CreateTeams("A", "B", "C", "D");
        var (a, b, c) = (teams[0], teams[1], teams[2]);

        // Cykl A > B > C > A: trzy drużyny mają po 3 punkty, bilans 0 i po 1 bramce, a mecze bezpośrednie też je równają
        var rows = StandingsCalculator.Calculate(teams, [Finished(a, b, 1, 0), Finished(b, c, 1, 0), Finished(c, a, 1, 0)]);

        Assert.Equal(["1:A", "1:B", "1:C", "4:D"], Describe(rows, labels));
    }

    [Fact]
    public void Calculate_IgnoresMatchesThatAreNotFinished()
    {
        var (teams, _) = CreateTeams("A", "B");
        var scheduled = new Match(TournamentId, 1, teams[0], teams[1]);
        var inProgress = new Match(TournamentId, 1, teams[0], teams[1]);
        inProgress.StartMatch();
        var cancelled = new Match(TournamentId, 1, teams[0], teams[1]);
        cancelled.Cancel();

        var rows = StandingsCalculator.Calculate(teams, [scheduled, inProgress, cancelled]);

        Assert.All(rows, row => Assert.Equal(0, row.Played));
    }

    [Fact]
    public void Calculate_IgnoresCancelledMatchEvenIfItAlreadyHadAScore()
    {
        var (teams, _) = CreateTeams("A", "B");
        var match = Finished(teams[0], teams[1], 4, 0);
        match.Cancel();

        var rows = StandingsCalculator.Calculate(teams, [match]);

        Assert.All(rows, row => Assert.Equal(0, row.Played));
    }

    [Fact]
    public void Calculate_IgnoresMatchOfTeamAgainstItself()
    {
        var (teams, _) = CreateTeams("A", "B");

        var rows = StandingsCalculator.Calculate(teams, [Finished(teams[0], teams[0], 2, 1)]);

        Assert.All(rows, row => Assert.Equal(0, row.Played));
    }

    [Fact]
    public void Calculate_IgnoresMatchesWithTeamsOutsideTheTable()
    {
        var (teams, _) = CreateTeams("A", "B");
        var outsider = Guid.NewGuid();

        var rows = StandingsCalculator.Calculate(teams, [Finished(teams[0], outsider, 5, 0), Finished(outsider, teams[1], 3, 0)]);

        Assert.All(rows, row => Assert.Equal(0, row.Played));
    }

    [Fact]
    public void Calculate_IgnoresMatchesWithoutBothTeams()
    {
        var (teams, _) = CreateTeams("A", "B");
        var withoutAway = new Match(TournamentId, 1, teams[0], awayTeamId: null);
        withoutAway.SetResult(1, 0);

        var rows = StandingsCalculator.Calculate(teams, [withoutAway]);

        Assert.All(rows, row => Assert.Equal(0, row.Played));
    }

    private static (List<Guid> Teams, Dictionary<Guid, string> Labels) CreateTeams(params string[] labels)
    {
        var teams = labels.Select(_ => Guid.NewGuid()).ToList();

        return (teams, teams.Zip(labels).ToDictionary(pair => pair.First, pair => pair.Second));
    }

    private static Match Finished(Guid home, Guid away, int homeScore, int awayScore)
    {
        var match = new Match(TournamentId, 1, home, away);
        match.SetResult(homeScore, awayScore);

        return match;
    }

    /// <summary>Wiersze jako "miejsce:drużyna", np. "1:A".</summary>
    private static IEnumerable<string> Describe(IReadOnlyList<StandingsRow> rows, Dictionary<Guid, string> labels) =>
        rows.Select(row => $"{row.Place}:{labels[row.TeamId]}");
}
