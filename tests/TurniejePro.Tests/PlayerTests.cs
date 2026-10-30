using TurniejePro.Domain;
using Xunit;

namespace TurniejePro.Tests;

public class PlayerTests
{
    [Fact]
    public void Constructor_ValidArguments_ShouldSetProperties()
    {
        var teamId = Guid.NewGuid();

        var player = new Player("Adam Nowak", teamId, 7);

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

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100)]
    public void Constructor_InvalidJerseyNumber_ShouldThrowArgumentOutOfRangeException(int invalidNumber)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Player("Adam", Guid.NewGuid(), invalidNumber));
    }

    [Fact]
    public void Update_ValidArguments_ShouldUpdateFullNameAndJerseyNumber()
    {
        var player = new Player("Adam", Guid.NewGuid(), 10);

        player.Update("Adam Nowak", 11);

        Assert.Equal("Adam Nowak", player.FullName);
        Assert.Equal(11, player.JerseyNumber);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Update_EmptyFullName_ShouldThrowArgumentException(string? invalidName)
    {
        var player = new Player("Adam", Guid.NewGuid(), 10);

        Assert.Throws<ArgumentException>(() => player.Update(invalidName!, 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(120)]
    public void Update_InvalidJerseyNumber_ShouldThrowArgumentOutOfRangeException(int invalidNumber)
    {
        var player = new Player("Adam", Guid.NewGuid(), 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => player.Update("Adam", invalidNumber));
    }
}
