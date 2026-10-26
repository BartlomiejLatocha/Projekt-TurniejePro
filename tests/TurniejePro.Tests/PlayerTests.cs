using TurniejePro.Domain;
using Xunit;

namespace TurniejePro.Tests;

public class PlayerTests
{
    [Fact]
    public void Constructor_ValidArguments_ShouldSetProperties()
    {
        // Arrange
        var teamId = Guid.NewGuid();

        // Act
        var player = new Player("Adam Nowak", teamId, 7);

        // Assert
        Assert.Equal("Adam Nowak", player.FullName);
        Assert.Equal(teamId, player.TeamId);
        Assert.Equal(7, player.JerseyNumber);
        Assert.NotEqual(Guid.Empty, player.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_EmptyFullName_ShouldThrowArgumentException(string? invalidName)
    {
        Assert.Throws<ArgumentException>(() => new Player(invalidName!, Guid.NewGuid()));
    }
}
