using TurniejePro.Domain;
using TurniejePro.Domain.Scheduling;
using TurniejePro.Domain.Standings;

namespace TurniejePro.Tests;

/// <summary>
/// Scenariusze dla tabeli ligowej (KAN-89): remisy w kolejności tabeli, korekta wyniku oraz własności tabeli
/// całej ligi liczonej z terminarza round-robin.
/// </summary>
public class StandingsScenarioTests
{
    private static readonly Guid TournamentId = Guid.NewGuid();

    public static TheoryData<int> LeagueSizes
    {
        get
        {
            var data = new TheoryData<int>();

            for (var size = 2; size <= 16; size++)
            {
                data.Add(size);
            }

            return data;
        }
    }

    [Fact]
    public void Tie_ThreeTeamsEqualOverall_AreOrderedByPointsFromMatchesBetweenThem()
    {
        // C, B, A mają po 4 punkty, bilans 0 i 1 bramkę. Mecze między nimi: A pokonała B, B pokonała C, A zremisowała z C
        // (punkty bezpośrednie: A 4, B 3, C 1). Lista wejściowa jest w odwrotnej kolejności, więc liczą się mecze bezpośrednie.
        // Mecze z X (spoza grupy) nie wchodzą do punktów bezpośrednich: gdyby weszły, wszystkie trzy miałyby po 4.
        var (teams, labels) = CreateTeams("C", "B", "A", "X", "Y");
        var (c, b, a, x, y) = (teams[0], teams[1], teams[2], teams[3], teams[4]);

        var rows = StandingsCalculator.Calculate(
            teams,
            [
                Finished(a, b, 1, 0),
                Finished(b, c, 1, 0),
                Finished(a, c, 0, 0),
                Finished(x, a, 1, 0),
                Finished(b, x, 0, 0),
                Finished(c, x, 1, 0),
                Finished(x, y, 5, 0)
            ]);

        Assert.Equal(["1:X", "2:A", "3:B", "4:C", "5:Y"], Describe(rows, labels));
        Assert.All(rows.Where(row => row.TeamId != x && row.TeamId != y), row => Assert.Equal((4, 0, 1), (row.Points, row.GoalDifference, row.GoalsFor)));
    }

    [Fact]
    public void Tie_TwoTeamsEqualOverallWithDrawBetweenThem_ShareThePlace()
    {
        var (teams, labels) = CreateTeams("A", "B", "C");
        var (a, b, c) = (teams[0], teams[1], teams[2]);

        var rows = StandingsCalculator.Calculate(teams, [Finished(a, b, 1, 1), Finished(a, c, 2, 0), Finished(b, c, 2, 0)]);

        // A i B: 4 punkty, bilans +2, 3 bramki, a mecz bezpośredni zremisowany. Po wspólnym miejscu 1 następne to 3
        Assert.Equal(["1:A", "1:B", "3:C"], Describe(rows, labels));
    }

    [Fact]
    public void Correction_ChangingTheScore_RecalculatesTheTable()
    {
        var (teams, labels) = CreateTeams("A", "B");
        var match = Finished(teams[0], teams[1], 2, 0);
        Assert.Equal(["1:A", "2:B"], Describe(StandingsCalculator.Calculate(teams, [match]), labels));

        match.SetResult(0, 1);

        var rows = StandingsCalculator.Calculate(teams, [match]);
        Assert.Equal(["1:B", "2:A"], Describe(rows, labels));
        Assert.Equal([3, 0], rows.Select(row => row.Points));
    }

    [Fact]
    public void Correction_TurningWinIntoDraw_SplitsPointsAndSharesThePlace()
    {
        var (teams, labels) = CreateTeams("A", "B");
        var match = Finished(teams[0], teams[1], 2, 0);

        match.SetResult(1, 1);

        var rows = StandingsCalculator.Calculate(teams, [match]);
        Assert.Equal(["1:A", "1:B"], Describe(rows, labels));
        Assert.All(rows, row => Assert.Equal((1, 0, 1), (row.Points, row.Won, row.Drawn)));
    }

    [Fact]
    public void Correction_ChangingTheScore_ChangesTieBreakOutcome()
    {
        var (teams, labels) = CreateTeams("A", "B", "C");
        var (a, b, c) = (teams[0], teams[1], teams[2]);
        var aBeatsC = Finished(a, c, 3, 0);
        var matches = new List<Match> { aBeatsC, Finished(b, c, 1, 0) };
        Assert.Equal(["1:A", "2:B", "3:C"], Describe(StandingsCalculator.Calculate(teams, matches), labels));

        // Po korekcie 3:0 na 1:0 A i B mają ten sam bilans i te same bramki, a nie grały ze sobą
        aBeatsC.SetResult(1, 0);

        Assert.Equal(["1:A", "1:B", "3:C"], Describe(StandingsCalculator.Calculate(teams, matches), labels));
    }

    [Fact]
    public void Correction_CalculatingTwice_GivesTheSameTableAndDoesNotChangeMatches()
    {
        var (teams, labels) = CreateTeams("A", "B", "C");
        var matches = new List<Match> { Finished(teams[0], teams[1], 2, 1), Finished(teams[1], teams[2], 0, 0) };

        var first = StandingsCalculator.Calculate(teams, matches);
        var second = StandingsCalculator.Calculate(teams, matches);

        Assert.Equal(Describe(first, labels), Describe(second, labels));
        Assert.Equal(first.Select(row => row.Points), second.Select(row => row.Points));
        Assert.Equal([(2, 1), (0, 0)], matches.Select(match => (match.HomeScore!.Value, match.AwayScore!.Value)));
    }

    [Theory]
    [MemberData(nameof(LeagueSizes))]
    public void FullLeague_EveryTeamPlaysEveryOtherTeamAndTotalsAreConsistent(int teamCount)
    {
        var (teams, matches) = CreateLeague(teamCount);

        var rows = StandingsCalculator.Calculate(teams, matches);

        Assert.Equal(teams.Order(), rows.Select(row => row.TeamId).Order());
        Assert.All(rows, row => Assert.Equal(teamCount - 1, row.Played));
        Assert.Equal(rows.Sum(row => row.Won), rows.Sum(row => row.Lost));
        Assert.Equal(rows.Sum(row => row.GoalsFor), rows.Sum(row => row.GoalsAgainst));
        Assert.All(rows, row => Assert.Equal(row.Played, row.Won + row.Drawn + row.Lost));
    }

    [Theory]
    [MemberData(nameof(LeagueSizes))]
    public void FullLeague_TotalPointsMatchTheNumberOfDecisiveMatchesAndDraws(int teamCount)
    {
        var (teams, matches) = CreateLeague(teamCount);

        var rows = StandingsCalculator.Calculate(teams, matches);

        var draws = matches.Count(match => match.HomeScore == match.AwayScore);
        var decisive = matches.Count - draws;
        Assert.Equal((decisive * StandingsCalculator.PointsForWin) + (draws * 2 * StandingsCalculator.PointsForDraw), rows.Sum(row => row.Points));
    }

    [Theory]
    [MemberData(nameof(LeagueSizes))]
    public void FullLeague_RowsAreSortedByPointsThenGoalDifferenceThenGoals(int teamCount)
    {
        var (teams, matches) = CreateLeague(teamCount);

        var rows = StandingsCalculator.Calculate(teams, matches);

        for (var i = 1; i < rows.Count; i++)
        {
            var previous = rows[i - 1];
            var current = rows[i];

            Assert.True(
                (previous.Points, previous.GoalDifference, previous.GoalsFor).CompareTo((current.Points, current.GoalDifference, current.GoalsFor)) >= 0,
                $"Wiersz {i + 1} jest wyżej niż powinien.");
        }
    }

    [Theory]
    [MemberData(nameof(LeagueSizes))]
    public void FullLeague_PlacesStartAtOneAndAreSharedOnlyByAdjacentRows(int teamCount)
    {
        var (teams, matches) = CreateLeague(teamCount);

        var rows = StandingsCalculator.Calculate(teams, matches);

        Assert.Equal(1, rows[0].Place);
        for (var i = 1; i < rows.Count; i++)
        {
            Assert.True(rows[i].Place == rows[i - 1].Place || rows[i].Place == i + 1, $"Wiersz {i + 1} ma nieprawidłowe miejsce {rows[i].Place}.");
        }
    }

    [Theory]
    [MemberData(nameof(LeagueSizes))]
    public void FullLeague_ShuffledMatchOrderGivesTheSameTable(int teamCount)
    {
        var (teams, matches) = CreateLeague(teamCount);

        var normal = StandingsCalculator.Calculate(teams, matches);
        var reversed = StandingsCalculator.Calculate(teams, matches.AsEnumerable().Reverse());

        Assert.Equal(
            normal.Select(row => (row.Place, row.TeamId, row.Points, row.GoalDifference)),
            reversed.Select(row => (row.Place, row.TeamId, row.Points, row.GoalDifference)));
    }

    /// <summary>Liga z terminarza round-robin i deterministycznymi, zróżnicowanymi wynikami (są wygrane, remisy i porażki).</summary>
    private static (List<Guid> Teams, List<Match> Matches) CreateLeague(int teamCount)
    {
        var teams = Enumerable.Range(0, teamCount).Select(_ => Guid.NewGuid()).ToList();
        var index = teams.Select((team, position) => (team, position)).ToDictionary(item => item.team, item => item.position);

        var matches = RoundRobinScheduler.Generate(teams)
            .SelectMany(round => round.Pairings)
            .Select(pairing =>
            {
                var home = index[pairing.HomeTeamId];
                var away = index[pairing.AwayTeamId];

                return Finished(pairing.HomeTeamId, pairing.AwayTeamId, ((home * 3) + away) % 4, (home + (away * 2)) % 3);
            })
            .ToList();

        return (teams, matches);
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
