using TurniejePro.Domain;
using Xunit;

namespace TurniejePro.Tests;

public class TeamTests
{
    [Fact]
    public void Constructor_ValidArguments_ShouldSetProperties()
    {
        // Arrange
        var tournamentId = Guid.NewGuid();
        var captainId = Guid.NewGuid();

        // Act
        var team = new Team("Orły", tournamentId, captainId, "kontakt@orly.pl");

        // Assert
        Assert.Equal("Orły", team.Name);
        Assert.Equal(tournamentId, team.TournamentId);
        Assert.Equal(captainId, team.CaptainId);
        Assert.Equal("kontakt@orly.pl", team.ContactEmail);
        Assert.False(team.IsAccepted);
        Assert.NotEqual(Guid.Empty, team.Id);
        Assert.Empty(team.Players);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_EmptyName_ShouldThrowArgumentException(string? invalidName)
    {
        Assert.Throws<ArgumentException>(() => new Team(invalidName!, Guid.NewGuid()));
    }

    [Fact]
    public void Accept_ShouldSetIsAcceptedToTrue()
    {
        // Arrange
        var team = new Team("Orły", Guid.NewGuid());

        // Act
        team.Accept();

        // Assert
        Assert.True(team.IsAccepted);
    }

    [Fact]
    public void Reject_ShouldSetIsAcceptedToFalse()
    {
        // Arrange
        var team = new Team("Orły", Guid.NewGuid());
        team.Accept();

        // Act
        team.Reject();

        // Assert
        Assert.False(team.IsAccepted);
    }

    [Fact]
    public void AddPlayer_ShouldAppendPlayerToCollection()
    {
        // Arrange
        var team = new Team("Orły", Guid.NewGuid());

        // Act
        var player = team.AddPlayer("Jan Kowalski", 10);

        // Assert
        Assert.Single(team.Players);
        Assert.Contains(player, team.Players);
        Assert.Equal("Jan Kowalski", player.FullName);
        Assert.Equal(10, player.JerseyNumber);
        Assert.Equal(team.Id, player.TeamId);
    }
}
