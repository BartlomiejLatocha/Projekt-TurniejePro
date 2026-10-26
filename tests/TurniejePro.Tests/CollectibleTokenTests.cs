using TurniejePro.Domain;
using Xunit;

namespace TurniejePro.Tests;

public class CollectibleTokenTests
{
    [Fact]
    public void Constructor_ValidArguments_ShouldSetPropertiesAndUppercaseCode()
    {
        // Arrange
        var tournamentId = Guid.NewGuid();

        // Act
        var token = new CollectibleToken("fan2026", "Odznaka Kibica", tournamentId, "Opis tokenu");

        // Assert
        Assert.Equal("FAN2026", token.Code);
        Assert.Equal("Odznaka Kibica", token.Name);
        Assert.Equal(tournamentId, token.TournamentId);
        Assert.Equal("Opis tokenu", token.Description);
        Assert.NotEqual(Guid.Empty, token.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_EmptyCode_ShouldThrowArgumentException(string? invalidCode)
    {
        Assert.Throws<ArgumentException>(() => new CollectibleToken(invalidCode!, "Nazwa", Guid.NewGuid()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_EmptyName_ShouldThrowArgumentException(string? invalidName)
    {
        Assert.Throws<ArgumentException>(() => new CollectibleToken("CODE", invalidName!, Guid.NewGuid()));
    }
}
