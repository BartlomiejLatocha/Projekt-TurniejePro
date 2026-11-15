using TurniejePro.Domain;
using TurniejePro.Domain.Standings;

namespace TurniejePro.Tests;

/// <summary>
/// Tabela liczona według zasad punktacji turnieju (KAN-300): punkty za wygraną, remis i porażkę oraz mecze
/// bezpośrednie pochodzą z <see cref="ScoringRules"/>.
/// </summary>
public class StandingsScoringRulesTests
{
    private static readonly Guid TournamentId = Guid.NewGuid();

    [Fact]
    public void Calculate_NullScoringRules_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => StandingsCalculator.Calculate([Guid.NewGuid()], [], null!));
    }

    [Fact]
    public void Calculate_WithoutRules_UsesDefaultScoring()
    {
        var (teams, matches) = SingleMatch(2, 1);

        var withoutRules = StandingsCalculator.Calculate(teams, matches);
        var withDefaultRules = StandingsCalculator.Calculate(teams, matches, ScoringRules.Default);

        Assert.Equal(withDefaultRules.Select(row => (row.TeamId, row.Points)), withoutRules.Select(row => (row.TeamId, row.Points)));
        Assert.Equal([3, 0], withoutRules.Select(row => row.Points));
    }

    [Theory]
    [InlineData(2, 1, 0, 2, 0)]
    [InlineData(3, 0, 0, 3, 0)]
    [InlineData(3, 1, 1, 3, 1)]
    [InlineData(5, 2, 3, 5, 3)]
    public void Calculate_Win_GivesPointsForWinToWinnerAndPointsForLossToLoser(int win, int draw, int loss, int expectedWinner, int expectedLoser)
    {
        var (teams, matches) = SingleMatch(2, 1);

        var rows = StandingsCalculator.Calculate(teams, matches, new ScoringRules(win, draw, loss));

        Assert.Equal([expectedWinner, expectedLoser], rows.Select(row => row.Points));
    }

    [Theory]
    [InlineData(3, 1, 0, 1)]
    [InlineData(3, 0, 0, 0)]
    [InlineData(2, 2, 1, 2)]
    public void Calculate_Draw_GivesPointsForDrawToBothTeams(int win, int draw, int loss, int expectedPoints)
    {
        var (teams, matches) = SingleMatch(1, 1);

        var rows = StandingsCalculator.Calculate(teams, matches, new ScoringRules(win, draw, loss));

        Assert.All(rows, row => Assert.Equal(expectedPoints, row.Points));
    }

    [Fact]
    public void Calculate_RulesChangeTheOrderOfTheTable()
    {
        // A: wygrana z C i porażka z D. B: dwa remisy (z C i z D). C: porażka z A i remis z B. D: wygrana z A i remis z B.
        var (teams, labels) = CreateTeams("A", "B", "C", "D");
        var (a, b, c, d) = (teams[0], teams[1], teams[2], teams[3]);
        var matches = new List<Match>
        {
            Finished(a, c, 1, 0),
            Finished(d, a, 1, 0),
            Finished(b, c, 0, 0),
            Finished(b, d, 0, 0)
        };

        var standard = StandingsCalculator.Calculate(teams, matches, new ScoringRules(3, 1, 0));
        var drawsWorthAsMuchAsWins = StandingsCalculator.Calculate(teams, matches, new ScoringRules(1, 1, 0));

        // Przy 3-1-0 wygrana jest warta więcej niż remisy: D 4, A 3, B 2, C 1
        Assert.Equal(["1:D", "2:A", "3:B", "4:C"], Describe(standard, labels));
        // Przy 1-1-0 dwa remisy biją wygraną z porażką: D i B po 2 punkty (D ma lepszy bilans), A i C po 1 (A ma lepszy bilans)
        Assert.Equal(["1:D", "2:B", "3:A", "4:C"], Describe(drawsWorthAsMuchAsWins, labels));
    }

    [Fact]
    public void Calculate_HeadToHeadUsesTheScoringRules()
    {
        // B jest przed A na liście wejściowej. A pokonała B, więc przy zwykłej punktacji A jest wyżej.
        var (teams, labels) = CreateTeams("B", "A", "X", "Y");
        var (b, a, x, y) = (teams[0], teams[1], teams[2], teams[3]);
        var matches = new List<Match> { Finished(a, b, 1, 0), Finished(a, x, 0, 1), Finished(b, y, 1, 0) };

        var standard = StandingsCalculator.Calculate(teams, matches, new ScoringRules(3, 1, 0));
        var worthNothing = StandingsCalculator.Calculate(teams, matches, new ScoringRules(0, 0, 0));

        Assert.Equal(["1:X", "2:A", "3:B", "4:Y"], Describe(standard, labels));
        // Bez punktów za cokolwiek mecz bezpośredni nie rozstrzyga: A i B (równe bilans i bramki) dzielą miejsce
        Assert.Equal(["1:X", "2:B", "2:A", "4:Y"], Describe(worthNothing, labels));
    }

    [Fact]
    public void Calculate_HeadToHeadCountsPointsForALoss()
    {
        // Ten sam układ meczów co wyżej, ale porażka jest warta tyle samo co wygrana (3-1-3).
        // A: wygrana z B i porażka z X, B: porażka z A i wygrana z Y, więc obie mają po 6 punktów, bilans 0 i 1 bramkę.
        var (teams, labels) = CreateTeams("B", "A", "X", "Y");
        var (b, a, x, y) = (teams[0], teams[1], teams[2], teams[3]);
        var matches = new List<Match> { Finished(a, b, 1, 0), Finished(a, x, 0, 1), Finished(b, y, 1, 0) };

        var rows = StandingsCalculator.Calculate(teams, matches, new ScoringRules(3, 1, 3));

        // W meczu bezpośrednim A (wygrana) i B (porażka) dostają po 3 punkty, więc dzielą miejsce. Z punktami za porażkę
        // równymi zeru A byłaby wyżej.
        Assert.Equal(["1:B", "1:A", "3:X", "4:Y"], Describe(rows, labels));
    }

    [Fact]
    public void Calculate_DrawInTournamentWithoutDraws_IsStillCounted()
    {
        var (teams, matches) = SingleMatch(1, 1);

        var rows = StandingsCalculator.Calculate(teams, matches, new ScoringRules(3, 1, 0, allowDraws: false));

        Assert.All(rows, row => Assert.Equal((1, 1), (row.Drawn, row.Points)));
    }

    private static (List<Guid> Teams, List<Match> Matches) SingleMatch(int homeScore, int awayScore)
    {
        var teams = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        return (teams, [Finished(teams[0], teams[1], homeScore, awayScore)]);
    }

    private static (List<Guid> Teams, Dictionary<Guid, string> Labels) CreateTeams(params string[] labels)
    {
        var teams = labels.Select(_ => Guid.NewGuid()).ToList();

        return (teams, teams.Zip(labels).ToDictionary(pair => pair.First, pair => pair.Second));
    }

    private static Match Finished(Guid home, Guid away, int homeScore, int awayScore)
    {
        var match = new Match(TournamentId, 1, 1, home, away);
        match.SetResult(homeScore, awayScore);

        return match;
    }

    private static IEnumerable<string> Describe(IReadOnlyList<StandingsRow> rows, Dictionary<Guid, string> labels) =>
        rows.Select(row => $"{row.Place}:{labels[row.TeamId]}");
}
