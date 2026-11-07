using TurniejePro.Domain.Scheduling;

namespace TurniejePro.Tests;

/// <summary>
/// Sprawdza własności terminarza ligowego dla wszystkich rozmiarów turnieju z zadania KAN-80:
/// od 2 do 16 drużyn oraz 64 drużyny. Każdy test dotyczy jednej własności, więc błąd wskazuje, co dokładnie się rozjechało.
/// </summary>
public class RoundRobinScheduleInvariantsTests
{
    public static TheoryData<int> TeamCounts => ToTheoryData(AllTeamCounts());

    public static TheoryData<int> EvenTeamCounts => ToTheoryData(AllTeamCounts().Where(count => count % 2 == 0));

    public static TheoryData<int> OddTeamCounts => ToTheoryData(AllTeamCounts().Where(count => count % 2 == 1));

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void NoPairOfTeamsMeetsTwice(int teamCount)
    {
        var (teams, rounds) = Generate(teamCount);

        var pairs = PairsOf(teams, rounds);

        Assert.Equal(pairs.Count, pairs.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void EveryPairOfTeamsMeetsExactlyOnce(int teamCount)
    {
        var (teams, rounds) = Generate(teamCount);

        var pairs = PairsOf(teams, rounds);

        var allPairs = from first in Enumerable.Range(0, teamCount)
                       from second in Enumerable.Range(first + 1, teamCount - first - 1)
                       select (first, second);
        Assert.Equal(allPairs.Order(), pairs.Order());
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void NoTeamPlaysItself(int teamCount)
    {
        var (_, rounds) = Generate(teamCount);

        Assert.All(rounds.SelectMany(round => round.Pairings), pairing => Assert.NotEqual(pairing.HomeTeamId, pairing.AwayTeamId));
    }

    [Theory]
    [MemberData(nameof(EvenTeamCounts))]
    public void EvenNumberOfTeams_HasNMinusOneRoundsWithHalfOfTeamsPlaying(int teamCount)
    {
        var (_, rounds) = Generate(teamCount);

        Assert.Equal(teamCount - 1, rounds.Count);
        Assert.All(rounds, round => Assert.Equal(teamCount / 2, round.Pairings.Count));
    }

    [Theory]
    [MemberData(nameof(OddTeamCounts))]
    public void OddNumberOfTeams_HasNRoundsWithOneTeamLeftOut(int teamCount)
    {
        var (_, rounds) = Generate(teamCount);

        Assert.Equal(teamCount, rounds.Count);
        Assert.All(rounds, round => Assert.Equal(teamCount / 2, round.Pairings.Count));
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void NoTeamPlaysTwiceInOneRound(int teamCount)
    {
        var (_, rounds) = Generate(teamCount);

        Assert.All(rounds, round =>
        {
            var playing = round.Pairings.SelectMany(pairing => new[] { pairing.HomeTeamId, pairing.AwayTeamId }).ToList();

            Assert.Equal(playing.Count, playing.Distinct().Count());
        });
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void EveryTeamPlaysAgainstEachOtherTeamSoNMinusOneMatches(int teamCount)
    {
        var (teams, rounds) = Generate(teamCount);
        var pairings = rounds.SelectMany(round => round.Pairings).ToList();

        Assert.All(teams, team =>
            Assert.Equal(teamCount - 1, pairings.Count(pairing => pairing.HomeTeamId == team || pairing.AwayTeamId == team)));
    }

    [Theory]
    [MemberData(nameof(EvenTeamCounts))]
    public void EvenNumberOfTeams_NoTeamRests(int teamCount)
    {
        var (_, rounds) = Generate(teamCount);

        Assert.All(rounds, round => Assert.Null(round.RestingTeamId));
    }

    [Theory]
    [MemberData(nameof(OddTeamCounts))]
    public void OddNumberOfTeams_EveryTeamRestsExactlyOnce(int teamCount)
    {
        var (teams, rounds) = Generate(teamCount);

        Assert.All(rounds, round => Assert.NotNull(round.RestingTeamId));
        Assert.Equal(teams.Order(), rounds.Select(round => round.RestingTeamId!.Value).Order());
    }

    [Theory]
    [MemberData(nameof(OddTeamCounts))]
    public void OddNumberOfTeams_RestingTeamDoesNotPlayInThatRound(int teamCount)
    {
        var (_, rounds) = Generate(teamCount);

        Assert.All(rounds, round =>
        {
            var playing = round.Pairings.SelectMany(pairing => new[] { pairing.HomeTeamId, pairing.AwayTeamId });

            Assert.DoesNotContain(round.RestingTeamId!.Value, playing);
        });
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void RoundsAreNumberedConsecutivelyFromOne(int teamCount)
    {
        var (_, rounds) = Generate(teamCount);

        Assert.Equal(Enumerable.Range(1, rounds.Count), rounds.Select(round => round.Number));
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void HomeAndAwayMatchesOfEveryTeamDifferByAtMostOne(int teamCount)
    {
        var (teams, rounds) = Generate(teamCount);
        var pairings = rounds.SelectMany(round => round.Pairings).ToList();

        Assert.All(teams, team =>
        {
            var home = pairings.Count(pairing => pairing.HomeTeamId == team);
            var away = pairings.Count(pairing => pairing.AwayTeamId == team);

            Assert.InRange(Math.Abs(home - away), 0, 1);
        });
    }

    private static IEnumerable<int> AllTeamCounts() => Enumerable.Range(2, 15).Append(64);

    private static TheoryData<int> ToTheoryData(IEnumerable<int> values)
    {
        var data = new TheoryData<int>();

        foreach (var value in values)
        {
            data.Add(value);
        }

        return data;
    }

    private static (List<Guid> Teams, IReadOnlyList<RoundRobinRound> Rounds) Generate(int teamCount)
    {
        var teams = Enumerable.Range(0, teamCount).Select(_ => Guid.NewGuid()).ToList();

        return (teams, RoundRobinScheduler.Generate(teams));
    }

    /// <summary>Pary jako indeksy drużyn (mniejszy, większy), bez rozróżniania gospodarza i gościa.</summary>
    private static List<(int First, int Second)> PairsOf(List<Guid> teams, IReadOnlyList<RoundRobinRound> rounds)
    {
        var index = teams.Select((team, position) => (team, position)).ToDictionary(item => item.team, item => item.position);

        return rounds
            .SelectMany(round => round.Pairings)
            .Select(pairing => (index[pairing.HomeTeamId], index[pairing.AwayTeamId]))
            .Select(pair => (Math.Min(pair.Item1, pair.Item2), Math.Max(pair.Item1, pair.Item2)))
            .ToList();
    }
}
