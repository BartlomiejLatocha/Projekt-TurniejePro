using TurniejePro.Domain.Scheduling;

namespace TurniejePro.Tests;

public class KnockoutBracketAdvancerTests
{
    public static TheoryData<int> TeamCounts
    {
        get
        {
            var data = new TheoryData<int>();

            foreach (var count in Enumerable.Range(2, 33).Concat([64, 100, 128]))
            {
                data.Add(count);
            }

            return data;
        }
    }

    [Fact]
    public void AdvanceWinner_NullRounds_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => KnockoutBracketAdvancer.AdvanceWinner(null!, 1, 1, Guid.NewGuid()));
    }

    [Fact]
    public void GetChampion_NullRounds_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => KnockoutBracketAdvancer.GetChampion(null!));
    }

    [Fact]
    public void AdvanceWinner_WinnerOfOddPosition_BecomesHomeTeamOfNextMatch()
    {
        var (teams, labels, bracket) = CreateBracket(4);

        var after = KnockoutBracketAdvancer.AdvanceWinner(bracket, 1, 1, teams[0]);

        Assert.Equal([["A vs D", "B vs C"], ["A vs ?"]], Describe(after, labels));
    }

    [Fact]
    public void AdvanceWinner_WinnerOfEvenPosition_BecomesAwayTeamOfNextMatch()
    {
        var (teams, labels, bracket) = CreateBracket(4);

        var after = KnockoutBracketAdvancer.AdvanceWinner(bracket, 1, 2, teams[2]);

        Assert.Equal([["A vs D", "B vs C"], ["? vs C"]], Describe(after, labels));
    }

    [Fact]
    public void AdvanceWinner_RecordsWinnerOnTheMatch()
    {
        var (teams, _, bracket) = CreateBracket(4);

        var after = KnockoutBracketAdvancer.AdvanceWinner(bracket, 1, 2, teams[2]);

        Assert.Null(after[0].Matches[0].WinnerId);
        Assert.Equal(teams[2], after[0].Matches[1].WinnerId);
    }

    [Fact]
    public void AdvanceWinner_BothSemifinalsAndFinal_ProducesChampion()
    {
        var (teams, labels, bracket) = CreateBracket(4);

        var afterSemifinals = KnockoutBracketAdvancer.AdvanceWinner(
            KnockoutBracketAdvancer.AdvanceWinner(bracket, 1, 1, teams[3]),
            1,
            2,
            teams[1]);
        Assert.Equal([["A vs D", "B vs C"], ["D vs B"]], Describe(afterSemifinals, labels));
        Assert.Null(KnockoutBracketAdvancer.GetChampion(afterSemifinals));

        var afterFinal = KnockoutBracketAdvancer.AdvanceWinner(afterSemifinals, 2, 1, teams[1]);

        Assert.Equal(teams[1], KnockoutBracketAdvancer.GetChampion(afterFinal));
    }

    [Fact]
    public void AdvanceWinner_DoesNotModifyTheOriginalBracket()
    {
        var (teams, labels, bracket) = CreateBracket(4);
        var before = Describe(bracket, labels);

        KnockoutBracketAdvancer.AdvanceWinner(bracket, 1, 1, teams[0]);

        Assert.Equal(before, Describe(bracket, labels));
        Assert.All(bracket.SelectMany(round => round.Matches), match => Assert.Null(match.WinnerId));
    }

    [Fact]
    public void AdvanceWinner_WinnerOfMatchAgainstByeOpponent_FillsTheFreeSlot()
    {
        // 5 drużyn: runda 2 ma już A (wolny los) oraz B i C (wolne losy), a D-E jest do rozegrania
        var (teams, labels, bracket) = CreateBracket(5);

        var after = KnockoutBracketAdvancer.AdvanceWinner(bracket, 1, 2, teams[4]);

        Assert.Equal([["A vs bye", "D vs E", "B vs bye", "C vs bye"], ["A vs E", "B vs C"], ["? vs ?"]], Describe(after, labels));
    }

    [Fact]
    public void AdvanceWinner_TeamThatIsNotInTheMatch_ThrowsArgumentException()
    {
        var (teams, _, bracket) = CreateBracket(4);

        Assert.Throws<ArgumentException>(() => KnockoutBracketAdvancer.AdvanceWinner(bracket, 1, 1, teams[1]));
    }

    [Fact]
    public void AdvanceWinner_ByeMatch_ThrowsInvalidOperationException()
    {
        var (teams, _, bracket) = CreateBracket(3);

        var exception = Assert.Throws<InvalidOperationException>(() => KnockoutBracketAdvancer.AdvanceWinner(bracket, 1, 1, teams[0]));

        // Wolny los nie ma gościa, więc samo "brak obu drużyn" też rzuciłoby wyjątek. Sprawdzamy, że przyczyną jest wolny los.
        Assert.Contains("wolnym losem", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AdvanceWinner_MatchWithoutBothTeams_ThrowsInvalidOperationException()
    {
        var (teams, _, bracket) = CreateBracket(4);

        Assert.Throws<InvalidOperationException>(() => KnockoutBracketAdvancer.AdvanceWinner(bracket, 2, 1, teams[0]));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(3, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 3)]
    public void AdvanceWinner_UnknownRoundOrPosition_ThrowsArgumentOutOfRangeException(int roundNumber, int position)
    {
        var (teams, _, bracket) = CreateBracket(4);

        Assert.Throws<ArgumentOutOfRangeException>(() => KnockoutBracketAdvancer.AdvanceWinner(bracket, roundNumber, position, teams[0]));
    }

    [Fact]
    public void AdvanceWinner_SameWinnerAgain_ChangesNothing()
    {
        var (teams, labels, bracket) = CreateBracket(4);
        var first = KnockoutBracketAdvancer.AdvanceWinner(bracket, 1, 1, teams[0]);

        var second = KnockoutBracketAdvancer.AdvanceWinner(first, 1, 1, teams[0]);

        Assert.Equal(Describe(first, labels), Describe(second, labels));
        Assert.Equal(teams[0], second[0].Matches[0].WinnerId);
    }

    [Fact]
    public void AdvanceWinner_CorrectionBeforeNextMatchIsDecided_ReplacesWinnerInNextRound()
    {
        var (teams, labels, bracket) = CreateBracket(4);
        var first = KnockoutBracketAdvancer.AdvanceWinner(bracket, 1, 1, teams[0]);

        var corrected = KnockoutBracketAdvancer.AdvanceWinner(first, 1, 1, teams[3]);

        Assert.Equal([["A vs D", "B vs C"], ["D vs ?"]], Describe(corrected, labels));
        Assert.Equal(teams[3], corrected[0].Matches[0].WinnerId);
    }

    [Fact]
    public void AdvanceWinner_CorrectionAfterNextMatchIsDecided_ThrowsInvalidOperationException()
    {
        var (teams, _, bracket) = CreateBracket(4);
        var decided = KnockoutBracketAdvancer.AdvanceWinner(
            KnockoutBracketAdvancer.AdvanceWinner(
                KnockoutBracketAdvancer.AdvanceWinner(bracket, 1, 1, teams[0]),
                1,
                2,
                teams[1]),
            2,
            1,
            teams[0]);

        Assert.Throws<InvalidOperationException>(() => KnockoutBracketAdvancer.AdvanceWinner(decided, 1, 1, teams[3]));
    }

    [Fact]
    public void AdvanceWinner_CorrectionOfTheFinal_ChangesTheChampion()
    {
        var (teams, _, bracket) = CreateBracket(2);
        var first = KnockoutBracketAdvancer.AdvanceWinner(bracket, 1, 1, teams[0]);

        var corrected = KnockoutBracketAdvancer.AdvanceWinner(first, 1, 1, teams[1]);

        Assert.Equal(teams[0], KnockoutBracketAdvancer.GetChampion(first));
        Assert.Equal(teams[1], KnockoutBracketAdvancer.GetChampion(corrected));
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void PlayingOutWholeBracket_HigherSeedAlwaysWins_TopSeedIsChampion(int teamCount)
    {
        var teams = CreateTeams(teamCount);

        var finished = PlayOut(KnockoutBracketGenerator.Generate(teams), (_, match) => match.HomeTeamId!.Value);

        Assert.Equal(teams[0], KnockoutBracketAdvancer.GetChampion(finished));
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void PlayingOutWholeBracket_EveryTeamExceptChampionLosesExactlyOnce(int teamCount)
    {
        var teams = CreateTeams(teamCount);

        // Zwycięzcy na przemian z domu i z wyjazdu, żeby awansowały też drużyny z dalszych miejsc drabinki
        var finished = PlayOut(
            KnockoutBracketGenerator.Generate(teams),
            (roundNumber, match) => (roundNumber + match.Position) % 2 == 0 ? match.HomeTeamId!.Value : match.AwayTeamId!.Value);

        var champion = KnockoutBracketAdvancer.GetChampion(finished);
        var played = finished.SelectMany(round => round.Matches).Where(match => !match.IsBye).ToList();
        Assert.NotNull(champion);
        Assert.Equal(teamCount - 1, played.Count);
        Assert.All(played, match => Assert.NotNull(match.WinnerId));

        var losers = played.Select(match => match.WinnerId == match.HomeTeamId ? match.AwayTeamId!.Value : match.HomeTeamId!.Value).ToList();
        Assert.Equal(teams.Where(team => team != champion).Order(), losers.Order());
    }

    private static (List<Guid> Teams, Dictionary<Guid, string> Labels, IReadOnlyList<BracketRound> Bracket) CreateBracket(int teamCount)
    {
        var teams = CreateTeams(teamCount);
        var labels = teams.Select((team, index) => (team, label: ((char)('A' + index)).ToString())).ToDictionary(item => item.team, item => item.label);

        return (teams, labels, KnockoutBracketGenerator.Generate(teams));
    }

    private static List<Guid> CreateTeams(int count) =>
        Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();

    /// <summary>Rozgrywa po kolei wszystkie mecze drabinki, a zwycięzcę każdego wybiera <paramref name="pickWinner"/>.</summary>
    private static IReadOnlyList<BracketRound> PlayOut(IReadOnlyList<BracketRound> bracket, Func<int, BracketMatch, Guid> pickWinner)
    {
        var current = bracket;

        for (var roundNumber = 1; roundNumber <= bracket.Count; roundNumber++)
        {
            for (var position = 1; position <= current[roundNumber - 1].Matches.Count; position++)
            {
                var match = current[roundNumber - 1].Matches[position - 1];

                if (!match.IsBye)
                {
                    current = KnockoutBracketAdvancer.AdvanceWinner(current, roundNumber, position, pickWinner(roundNumber, match));
                }
            }
        }

        return current;
    }

    /// <summary>Każdy mecz jako "A vs B": "?" to miejsce czekające na zwycięzcę, "bye" to wolny los.</summary>
    private static IEnumerable<string[]> Describe(IReadOnlyList<BracketRound> rounds, Dictionary<Guid, string> labels) =>
        rounds.Select(round => round.Matches
            .Select(match => $"{Label(match.HomeTeamId, labels)} vs {(match.IsBye ? "bye" : Label(match.AwayTeamId, labels))}")
            .ToArray());

    private static string Label(Guid? teamId, Dictionary<Guid, string> labels) =>
        teamId is { } id ? labels[id] : "?";
}
