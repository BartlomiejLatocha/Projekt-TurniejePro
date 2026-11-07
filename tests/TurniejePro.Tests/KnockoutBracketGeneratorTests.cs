using TurniejePro.Domain.Scheduling;

namespace TurniejePro.Tests;

public class KnockoutBracketGeneratorTests
{
    [Fact]
    public void Generate_NullTeams_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => KnockoutBracketGenerator.Generate(null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Generate_FewerThanTwoTeams_ThrowsArgumentException(int teamCount)
    {
        Assert.Throws<ArgumentException>(() => KnockoutBracketGenerator.Generate(CreateTeams(teamCount)));
    }

    [Fact]
    public void Generate_DuplicatedTeam_ThrowsArgumentException()
    {
        var team = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => KnockoutBracketGenerator.Generate([team, Guid.NewGuid(), team]));
    }

    [Fact]
    public void Generate_TwoTeams_ReturnsSingleFinal()
    {
        var teams = CreateTeams(2);
        var labels = LabelTeams(teams, "A", "B");

        var rounds = KnockoutBracketGenerator.Generate(teams);

        Assert.Equal([["A vs B"]], Describe(rounds, labels));
    }

    [Fact]
    public void Generate_FourTeams_PairsSeedsOneWithFourAndTwoWithThree()
    {
        var teams = CreateTeams(4);
        var labels = LabelTeams(teams, "A", "B", "C", "D");

        var rounds = KnockoutBracketGenerator.Generate(teams);

        Assert.Equal(
            [
                ["A vs D", "B vs C"],
                ["? vs ?"]
            ],
            Describe(rounds, labels));
    }

    [Fact]
    public void Generate_EightTeams_UsesStandardSeeding()
    {
        var teams = CreateTeams(8);
        var labels = LabelTeams(teams, "A", "B", "C", "D", "E", "F", "G", "H");

        var rounds = KnockoutBracketGenerator.Generate(teams);

        // Najwyżej rozstawieni (A i B) są w przeciwnych połówkach drabinki, więc spotkają się najwcześniej w finale
        Assert.Equal(
            [
                ["A vs H", "D vs E", "B vs G", "C vs F"],
                ["? vs ?", "? vs ?"],
                ["? vs ?"]
            ],
            Describe(rounds, labels));
    }

    [Fact]
    public void Generate_ThreeTeams_TopSeedGetsByeAndWaitsInFinal()
    {
        var teams = CreateTeams(3);
        var labels = LabelTeams(teams, "A", "B", "C");

        var rounds = KnockoutBracketGenerator.Generate(teams);

        Assert.Equal(
            [
                ["A vs bye", "B vs C"],
                ["A vs ?"]
            ],
            Describe(rounds, labels));
    }

    [Fact]
    public void Generate_FiveTeams_ThreeTopSeedsGetByes()
    {
        var teams = CreateTeams(5);
        var labels = LabelTeams(teams, "A", "B", "C", "D", "E");

        var rounds = KnockoutBracketGenerator.Generate(teams);

        Assert.Equal(
            [
                ["A vs bye", "D vs E", "B vs bye", "C vs bye"],
                ["A vs ?", "B vs C"],
                ["? vs ?"]
            ],
            Describe(rounds, labels));
    }

    [Fact]
    public void Generate_ByeMatchHasItsHomeTeamAsWinner()
    {
        var teams = CreateTeams(3);

        var rounds = KnockoutBracketGenerator.Generate(teams);

        var bye = rounds[0].Matches.Single(match => match.IsBye);
        Assert.Equal(teams[0], bye.WinnerId);
        Assert.Null(bye.AwayTeamId);
        Assert.All(rounds[0].Matches.Where(match => !match.IsBye), match => Assert.Null(match.WinnerId));
    }

    [Fact]
    public void Generate_RoundsAndPositionsAreNumberedFromOne()
    {
        var rounds = KnockoutBracketGenerator.Generate(CreateTeams(8));

        Assert.Equal([1, 2, 3], rounds.Select(round => round.Number));
        Assert.All(rounds, round => Assert.Equal(Enumerable.Range(1, round.Matches.Count), round.Matches.Select(match => match.Position)));
    }

    [Fact]
    public void Generate_SameInput_ReturnsSameBracket()
    {
        var teams = CreateTeams(6);
        var labels = LabelTeams(teams, "A", "B", "C", "D", "E", "F");

        var first = KnockoutBracketGenerator.Generate(teams);
        var second = KnockoutBracketGenerator.Generate(teams);

        Assert.Equal(Describe(first, labels), Describe(second, labels));
    }

    private static List<Guid> CreateTeams(int count) =>
        Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();

    private static Dictionary<Guid, string> LabelTeams(List<Guid> teams, params string[] labels) =>
        teams.Zip(labels).ToDictionary(pair => pair.First, pair => pair.Second);

    /// <summary>Każdy mecz jako "A vs B": "?" oznacza miejsce czekające na zwycięzcę, "bye" to wolny los.</summary>
    private static IEnumerable<string[]> Describe(IReadOnlyList<BracketRound> rounds, Dictionary<Guid, string> labels) =>
        rounds.Select(round => round.Matches
            .Select(match => $"{Label(match.HomeTeamId, labels)} vs {(match.IsBye ? "bye" : Label(match.AwayTeamId, labels))}")
            .ToArray());

    private static string Label(Guid? teamId, Dictionary<Guid, string> labels) =>
        teamId is { } id ? labels[id] : "?";
}
