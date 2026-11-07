using TurniejePro.Domain.Scheduling;

namespace TurniejePro.Tests;

public class RoundRobinSchedulerTests
{
    [Fact]
    public void Generate_NullTeams_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RoundRobinScheduler.Generate(null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Generate_FewerThanTwoTeams_ThrowsArgumentException(int teamCount)
    {
        Assert.Throws<ArgumentException>(() => RoundRobinScheduler.Generate(CreateTeams(teamCount)));
    }

    [Fact]
    public void Generate_DuplicatedTeam_ThrowsArgumentException()
    {
        var team = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => RoundRobinScheduler.Generate([team, Guid.NewGuid(), team]));
    }

    [Fact]
    public void Generate_TwoTeams_ReturnsOneRoundWithOneMatch()
    {
        var teams = CreateTeams(2);

        var rounds = RoundRobinScheduler.Generate(teams);

        var round = Assert.Single(rounds);
        var pairing = Assert.Single(round.Pairings);
        Assert.Equal(teams.Order(), new[] { pairing.HomeTeamId, pairing.AwayTeamId }.Order());
        Assert.Null(round.RestingTeamId);
    }

    [Fact]
    public void Generate_FourTeams_ReturnsEveryPairOnceInThreeRounds()
    {
        var teams = CreateTeams(4);
        var labels = LabelTeams(teams, "A", "B", "C", "D");

        var rounds = RoundRobinScheduler.Generate(teams);

        Assert.Equal(
            [
                ["A-D", "B-C"],
                ["A-C", "B-D"],
                ["A-B", "C-D"]
            ],
            rounds.Select(round => round.Pairings.Select(pairing => Label(pairing, labels)).Order().ToArray()));
    }

    [Fact]
    public void Generate_FourTeams_MatchesExampleFromDocumentation()
    {
        var teams = CreateTeams(4);
        var labels = LabelTeams(teams, "A", "B", "C", "D");

        var rounds = RoundRobinScheduler.Generate(teams);

        // Pierwsza w parze jest gospodarzem (docs/scheduling.md)
        Assert.Equal(
            [
                ["A-D", "B-C"],
                ["C-A", "D-B"],
                ["A-B", "C-D"]
            ],
            rounds.Select(round => round.Pairings.Select(pairing => $"{labels[pairing.HomeTeamId]}-{labels[pairing.AwayTeamId]}").ToArray()));
    }

    [Fact]
    public void Generate_ThreeTeams_MatchesExampleFromDocumentation()
    {
        var teams = CreateTeams(3);
        var labels = LabelTeams(teams, "A", "B", "C");

        var rounds = RoundRobinScheduler.Generate(teams);

        Assert.Equal(
            [("A-B", "C"), ("C-A", "B"), ("B-C", "A")],
            rounds.Select(round =>
            {
                var pairing = Assert.Single(round.Pairings);
                return ($"{labels[pairing.HomeTeamId]}-{labels[pairing.AwayTeamId]}", labels[round.RestingTeamId!.Value]);
            }));
    }

    [Fact]
    public void Generate_ThreeTeams_OneTeamRestsInEachRound()
    {
        var teams = CreateTeams(3);

        var rounds = RoundRobinScheduler.Generate(teams);

        Assert.Equal(3, rounds.Count);
        Assert.All(rounds, round =>
        {
            var pairing = Assert.Single(round.Pairings);
            Assert.NotNull(round.RestingTeamId);
            Assert.DoesNotContain(round.RestingTeamId.Value, new[] { pairing.HomeTeamId, pairing.AwayTeamId });
        });
        Assert.Equal(teams.Order(), rounds.Select(round => round.RestingTeamId!.Value).Order());
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void Generate_EvenNumberOfTeams_NoOneRests(int teamCount)
    {
        var rounds = RoundRobinScheduler.Generate(CreateTeams(teamCount));

        Assert.All(rounds, round => Assert.Null(round.RestingTeamId));
    }

    [Fact]
    public void Generate_RoundsAreNumberedFromOne()
    {
        var rounds = RoundRobinScheduler.Generate(CreateTeams(5));

        Assert.Equal([1, 2, 3, 4, 5], rounds.Select(round => round.Number));
    }

    [Fact]
    public void Generate_SameInput_ReturnsSameSchedule()
    {
        var teams = CreateTeams(7);

        var first = RoundRobinScheduler.Generate(teams);
        var second = RoundRobinScheduler.Generate(teams);

        Assert.Equal(Flatten(first), Flatten(second));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void Generate_HomeAndAwayMatchesAreBalanced(int teamCount)
    {
        var teams = CreateTeams(teamCount);

        var pairings = RoundRobinScheduler.Generate(teams).SelectMany(round => round.Pairings).ToList();

        Assert.All(teams, team =>
        {
            var home = pairings.Count(pairing => pairing.HomeTeamId == team);
            var away = pairings.Count(pairing => pairing.AwayTeamId == team);

            Assert.InRange(Math.Abs(home - away), 0, 1);
        });
    }

    private static List<Guid> CreateTeams(int count) =>
        Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();

    private static Dictionary<Guid, string> LabelTeams(List<Guid> teams, params string[] labels) =>
        teams.Zip(labels).ToDictionary(pair => pair.First, pair => pair.Second);

    /// <summary>Para jako "A-B" z etykiet w kolejności alfabetycznej, bez rozróżniania gospodarza i gościa.</summary>
    private static string Label(RoundRobinPairing pairing, Dictionary<Guid, string> labels) =>
        string.Join('-', new[] { labels[pairing.HomeTeamId], labels[pairing.AwayTeamId] }.Order());

    private static List<(int Round, Guid Home, Guid Away)> Flatten(IReadOnlyList<RoundRobinRound> rounds) =>
        rounds.SelectMany(round => round.Pairings.Select(pairing => (round.Number, pairing.HomeTeamId, pairing.AwayTeamId))).ToList();
}
