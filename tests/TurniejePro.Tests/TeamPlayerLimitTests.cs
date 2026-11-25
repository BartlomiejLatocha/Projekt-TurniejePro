using TurniejePro.Domain;
using TurniejePro.Domain.Exceptions;
using Xunit;

namespace TurniejePro.Tests;

public class TeamPlayerLimitTests
{
    private static Team CreateTeam() => new("Test", Guid.NewGuid());

    private static List<(string FullName, int? JerseyNumber)> Roster(int count) =>
        Enumerable.Range(1, count).Select(i => ($"Zawodnik {i}", (int?)null)).ToList();

    [Fact]
    public void ValidatePlayerCount_Zero_Throws()
    {
        Assert.Throws<BusinessRuleException>(() => Team.ValidatePlayerCount(0));
    }

    [Theory]
    [InlineData(Team.MinPlayers)]
    [InlineData(10)]
    [InlineData(Team.MaxPlayers)]
    public void ValidatePlayerCount_WithinRange_DoesNotThrow(int count)
    {
        Team.ValidatePlayerCount(count);
    }

    [Fact]
    public void ValidatePlayerCount_AboveMaximum_Throws()
    {
        Assert.Throws<BusinessRuleException>(() => Team.ValidatePlayerCount(Team.MaxPlayers + 1));
    }

    [Fact]
    public void ReplacePlayers_ReplacesWholeRoster()
    {
        var team = CreateTeam();
        team.ReplacePlayers(Roster(3));

        team.ReplacePlayers(Roster(2));

        Assert.Equal(2, team.Players.Count);
    }

    [Fact]
    public void ReplacePlayers_TooMany_LeavesRosterUnchanged()
    {
        var team = CreateTeam();
        team.ReplacePlayers(Roster(3));

        Assert.Throws<BusinessRuleException>(() => team.ReplacePlayers(Roster(Team.MaxPlayers + 1)));

        Assert.Equal(3, team.Players.Count);
    }

    [Fact]
    public void ReplacePlayers_Empty_Throws()
    {
        var team = CreateTeam();
        team.ReplacePlayers(Roster(2));

        Assert.Throws<BusinessRuleException>(() => team.ReplacePlayers([]));

        Assert.Equal(2, team.Players.Count);
    }

    [Fact]
    public void ReplacePlayers_DuplicateJerseyNumber_Throws()
    {
        var team = CreateTeam();

        Assert.Throws<BusinessRuleException>(() => team.ReplacePlayers(
        [
            ("Jan Kowalski", 7),
            ("Adam Nowak", 7)
        ]));
    }
}
