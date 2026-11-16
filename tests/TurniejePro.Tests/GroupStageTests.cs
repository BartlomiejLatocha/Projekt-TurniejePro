using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using TurniejePro.Domain.Scheduling;
using Xunit;

namespace TurniejePro.Tests;

public class GroupStageTests
{
    [Theory]
    [InlineData(2, 0)]
    [InlineData(5, 0)]
    [InlineData(6, 2)]
    [InlineData(8, 2)]
    [InlineData(12, 4)]
    [InlineData(16, 4)]
    [InlineData(24, 8)]
    public void GroupCountFor_ReturnsLargestPowerOfTwoWithAtLeastThreeTeamsPerGroup(int teams, int expected)
    {
        Assert.Equal(expected, GroupStage.GroupCountFor(teams));
    }

    [Fact]
    public void Distribute_SnakeOrder_BalancesGroups()
    {
        var seeded = Enumerable.Range(1, 8).Select(_ => Guid.NewGuid()).ToList();

        var groups = GroupStage.Distribute(seeded, 2);

        Assert.Equal(new[] { seeded[0], seeded[3], seeded[4], seeded[7] }, groups[0]);
        Assert.Equal(new[] { seeded[1], seeded[2], seeded[5], seeded[6] }, groups[1]);
    }

    [Fact]
    public void BuildKnockout_TwoGroups_PairsWinnerWithOpponentRunnerUp()
    {
        var tournamentId = Guid.NewGuid();
        var teams = Enumerable.Range(0, 6).Select(i => new Team($"T{i}", tournamentId) { GroupNumber = i % 2 == 0 ? 1 : 2 }).ToList();
        var groupA = teams.Where(t => t.GroupNumber == 1).ToList();
        var groupB = teams.Where(t => t.GroupNumber == 2).ToList();

        // Grupa A: a0 wygrywa wszystkie mecze, a1 drugi. Grupa B: b0 wygrywa, b1 drugi.
        var matches = new List<Match>();
        AddFinished(matches, tournamentId, 1, groupA[0].Id, groupA[1].Id, 3, 0);
        AddFinished(matches, tournamentId, 2, groupA[0].Id, groupA[2].Id, 2, 0);
        AddFinished(matches, tournamentId, 3, groupA[1].Id, groupA[2].Id, 1, 0);
        AddFinished(matches, tournamentId, 1, groupB[0].Id, groupB[1].Id, 2, 1);
        AddFinished(matches, tournamentId, 2, groupB[0].Id, groupB[2].Id, 4, 0);
        AddFinished(matches, tournamentId, 3, groupB[1].Id, groupB[2].Id, 1, 0);
        foreach (var match in matches)
        {
            match.IsGroupStage = true;
        }

        var knockout = GroupStage.BuildKnockout(tournamentId, teams, matches, ScoringRules.Default, 4);

        var firstRound = knockout.Where(m => m.Round == 4).OrderBy(m => m.Position).ToList();
        Assert.Equal(2, firstRound.Count);
        Assert.Equal(groupA[0].Id, firstRound[0].HomeTeamId);
        Assert.Equal(groupB[1].Id, firstRound[0].AwayTeamId);
        Assert.Equal(groupB[0].Id, firstRound[1].HomeTeamId);
        Assert.Equal(groupA[1].Id, firstRound[1].AwayTeamId);
        Assert.Single(knockout, m => m.Round == 5);
        Assert.All(knockout, m => Assert.False(m.IsGroupStage));
    }

    private static void AddFinished(List<Match> matches, Guid tournamentId, int round, Guid home, Guid away, int homeScore, int awayScore)
    {
        var match = new Match(tournamentId, round, matches.Count(m => m.Round == round) + 1, home, away);
        match.SetResult(homeScore, awayScore);
        matches.Add(match);
    }
}
