using TurniejePro.Domain.Scheduling;

namespace TurniejePro.Tests;

/// <summary>
/// Sprawdza własności drabinki pucharowej dla wielu rozmiarów (KAN-82): od 2 do 34 drużyn, czyli przez kilka
/// granic potęg dwójki (m.in. 4/5, 8/9, 16/17, 32/33), oraz 64, 100 i 128 drużyn (128 to limit turnieju).
/// Każdy test dotyczy jednej własności, więc błąd wskazuje, co dokładnie się rozjechało.
/// </summary>
public class KnockoutBracketInvariantsTests
{
    public static TheoryData<int> TeamCounts => ToTheoryData(AllTeamCounts());

    public static TheoryData<int> TeamCountsFromThree => ToTheoryData(AllTeamCounts().Where(count => count >= 3));

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void HasCeilingOfLogTwoRoundsEndingWithSingleFinal(int teamCount)
    {
        var (_, rounds) = Generate(teamCount);

        Assert.Equal(RoundCount(teamCount), rounds.Count);
        Assert.Single(rounds[^1].Matches);
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void EachRoundHasHalfAsManyMatchesAsThePreviousOne(int teamCount)
    {
        var (_, rounds) = Generate(teamCount);

        Assert.Equal(BracketSize(teamCount) / 2, rounds[0].Matches.Count);
        for (var i = 1; i < rounds.Count; i++)
        {
            Assert.Equal(rounds[i - 1].Matches.Count / 2, rounds[i].Matches.Count);
        }
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void RoundsAndPositionsAreNumberedConsecutivelyFromOne(int teamCount)
    {
        var (_, rounds) = Generate(teamCount);

        Assert.Equal(Enumerable.Range(1, rounds.Count), rounds.Select(round => round.Number));
        Assert.All(rounds, round => Assert.Equal(Enumerable.Range(1, round.Matches.Count), round.Matches.Select(match => match.Position)));
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void EveryTeamAppearsExactlyOnceInFirstRound(int teamCount)
    {
        var (teams, rounds) = Generate(teamCount);

        var inFirstRound = rounds[0].Matches
            .SelectMany(match => new[] { match.HomeTeamId, match.AwayTeamId })
            .OfType<Guid>()
            .ToList();

        Assert.Equal(teams.Order(), inFirstRound.Order());
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void NoTeamMeetsItself(int teamCount)
    {
        var (_, rounds) = Generate(teamCount);

        Assert.All(
            rounds.SelectMany(round => round.Matches).Where(match => match.HomeTeamId is not null && match.AwayTeamId is not null),
            match => Assert.NotEqual(match.HomeTeamId, match.AwayTeamId));
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void NumberOfByesIsBracketSizeMinusTeamCountAndTheyAreOnlyInFirstRound(int teamCount)
    {
        var (_, rounds) = Generate(teamCount);

        Assert.Equal(BracketSize(teamCount) - teamCount, rounds[0].Matches.Count(match => match.IsBye));
        Assert.All(rounds.Skip(1).SelectMany(round => round.Matches), match => Assert.False(match.IsBye));
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void ByesGoToTheTopSeeds(int teamCount)
    {
        var (teams, rounds) = Generate(teamCount);
        var byeCount = BracketSize(teamCount) - teamCount;

        var byeTeams = rounds[0].Matches.Where(match => match.IsBye).Select(match => match.HomeTeamId!.Value);

        Assert.Equal(teams.Take(byeCount).Order(), byeTeams.Order());
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void ByeMatchHasOnlyHomeTeamAndItIsTheWinner(int teamCount)
    {
        var (_, rounds) = Generate(teamCount);

        Assert.All(rounds[0].Matches.Where(match => match.IsBye), match =>
        {
            Assert.NotNull(match.HomeTeamId);
            Assert.Null(match.AwayTeamId);
            Assert.Equal(match.HomeTeamId, match.WinnerId);
        });
        Assert.All(rounds[0].Matches.Where(match => !match.IsBye), match => Assert.Null(match.WinnerId));
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void MatchesToPlayAreTeamCountMinusOne(int teamCount)
    {
        var (_, rounds) = Generate(teamCount);

        var matchesToPlay = rounds.SelectMany(round => round.Matches).Count(match => !match.IsBye);

        Assert.Equal(teamCount - 1, matchesToPlay);
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void HigherSeedIsHomeInEveryFirstRoundMatch(int teamCount)
    {
        var (teams, rounds) = Generate(teamCount);
        var index = IndexOf(teams);

        Assert.All(rounds[0].Matches.Where(match => !match.IsBye), match =>
            Assert.True(index[match.HomeTeamId!.Value] < index[match.AwayTeamId!.Value]));
    }

    [Theory]
    [MemberData(nameof(TeamCountsFromThree))]
    public void TwoTopSeedsAreInOppositeHalvesSoTheyCanMeetOnlyInFinal(int teamCount)
    {
        var (teams, rounds) = Generate(teamCount);
        var halfSize = rounds[0].Matches.Count / 2;

        var seedOnePosition = PositionInFirstRound(rounds, teams[0]);
        var seedTwoPosition = PositionInFirstRound(rounds, teams[1]);

        Assert.True(seedOnePosition <= halfSize);
        Assert.True(seedTwoPosition > halfSize);
    }

    [Theory]
    [MemberData(nameof(TeamCountsFromThree))]
    public void ByeTeamsAreAlreadyPlacedInSecondRound(int teamCount)
    {
        var (_, rounds) = Generate(teamCount);

        var placed = new List<Guid>();
        foreach (var bye in rounds[0].Matches.Where(match => match.IsBye))
        {
            var next = rounds[1].Matches[((bye.Position + 1) / 2) - 1];
            var slot = bye.Position % 2 == 1 ? next.HomeTeamId : next.AwayTeamId;

            Assert.Equal(bye.HomeTeamId, slot);
            placed.Add(slot!.Value);
        }

        var filledSlots = rounds[1].Matches.SelectMany(match => new[] { match.HomeTeamId, match.AwayTeamId }).OfType<Guid>().ToList();
        Assert.Equal(placed.Order(), filledSlots.Order());
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void NoTeamAppearsTwiceInOneRound(int teamCount)
    {
        var (_, rounds) = Generate(teamCount);

        Assert.All(rounds, round =>
        {
            var teamsInRound = round.Matches.SelectMany(match => new[] { match.HomeTeamId, match.AwayTeamId }).OfType<Guid>().ToList();

            Assert.Equal(teamsInRound.Count, teamsInRound.Distinct().Count());
        });
    }

    private static IEnumerable<int> AllTeamCounts() => Enumerable.Range(2, 33).Concat([64, 100, 128]);

    private static int BracketSize(int teamCount)
    {
        var size = 2;
        while (size < teamCount)
        {
            size *= 2;
        }

        return size;
    }

    private static int RoundCount(int teamCount)
    {
        var rounds = 1;
        for (var size = 2; size < teamCount; size *= 2)
        {
            rounds++;
        }

        return rounds;
    }

    private static int PositionInFirstRound(IReadOnlyList<BracketRound> rounds, Guid team) =>
        rounds[0].Matches.Single(match => match.HomeTeamId == team || match.AwayTeamId == team).Position;

    private static Dictionary<Guid, int> IndexOf(List<Guid> teams) =>
        teams.Select((team, position) => (team, position)).ToDictionary(item => item.team, item => item.position);

    private static TheoryData<int> ToTheoryData(IEnumerable<int> values)
    {
        var data = new TheoryData<int>();

        foreach (var value in values)
        {
            data.Add(value);
        }

        return data;
    }

    private static (List<Guid> Teams, IReadOnlyList<BracketRound> Rounds) Generate(int teamCount)
    {
        var teams = Enumerable.Range(0, teamCount).Select(_ => Guid.NewGuid()).ToList();

        return (teams, KnockoutBracketGenerator.Generate(teams));
    }
}
