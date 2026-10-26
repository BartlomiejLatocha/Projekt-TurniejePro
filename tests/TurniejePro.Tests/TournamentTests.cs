using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using Xunit;

namespace TurniejePro.Tests;

public class TournamentTests
{
    [Fact]
    public void TournamentShouldInitializeWithCorrectNameAndStatus()
    {
        // Arrange
        var tournamentName = "Mistrzostwa Polski w eSporcie";

        // Act
        var tournament = new Tournament(tournamentName);

        // Assert
        Assert.Equal(tournamentName, tournament.Name);
        Assert.False(tournament.IsStarted);
        Assert.Equal("Ogólna", tournament.Discipline);
        Assert.Equal(TournamentFormat.Cup, tournament.Format);
        Assert.Equal(16, tournament.MaxTeams);
        Assert.NotEqual(Guid.Empty, tournament.Id);
    }

    [Fact]
    public void FullConstructor_ValidArguments_ShouldSetAllProperties()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var startsAt = DateTimeOffset.UtcNow.AddDays(7);

        // Act
        var tournament = new Tournament(
            "Liga Szkolna",
            "Piłka Nożna",
            TournamentFormat.League,
            startsAt,
            8,
            organizerId);

        // Assert
        Assert.Equal("Liga Szkolna", tournament.Name);
        Assert.Equal("Piłka Nożna", tournament.Discipline);
        Assert.Equal(TournamentFormat.League, tournament.Format);
        Assert.Equal(startsAt, tournament.StartsAt);
        Assert.Equal(8, tournament.MaxTeams);
        Assert.Equal(organizerId, tournament.OrganizerId);
        Assert.False(tournament.IsStarted);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void FullConstructor_InvalidName_ShouldThrowArgumentException(string? invalidName)
    {
        Assert.Throws<ArgumentException>(() => new Tournament(
            invalidName!,
            "Szachy",
            TournamentFormat.Cup,
            DateTimeOffset.UtcNow,
            8));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-5)]
    public void FullConstructor_MaxTeamsLessThanTwo_ShouldThrowArgumentOutOfRangeException(int invalidMaxTeams)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Tournament(
            "Turniej",
            "Szachy",
            TournamentFormat.Cup,
            DateTimeOffset.UtcNow,
            invalidMaxTeams));
    }

    [Fact]
    public void Start_WhenNotStarted_ShouldSetIsStartedToTrue()
    {
        // Arrange
        var tournament = new Tournament("Turniej CS");

        // Act
        tournament.Start();

        // Assert
        Assert.True(tournament.IsStarted);
    }

    [Fact]
    public void Start_WhenAlreadyStarted_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var tournament = new Tournament("Turniej CS");
        tournament.Start();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => tournament.Start());
        Assert.Equal("Turniej został już rozpoczęty.", ex.Message);
    }

    [Fact]
    public void CanRegisterTeam_WhenNotStartedAndBelowLimit_ShouldReturnTrue()
    {
        // Arrange
        var tournament = new Tournament("Turniej CS");

        // Act & Assert
        Assert.True(tournament.CanRegisterTeam());
    }

    [Fact]
    public void CanRegisterTeam_WhenStarted_ShouldReturnFalse()
    {
        // Arrange
        var tournament = new Tournament("Turniej CS");
        tournament.Start();

        // Act & Assert
        Assert.False(tournament.CanRegisterTeam());
    }

    [Fact]
    public void CanRegisterTeam_WhenMaxTeamsReached_ShouldReturnFalse()
    {
        // Arrange
        var tournament = new Tournament("Turniej 2 Drużyn", "FIFA", TournamentFormat.Cup, DateTimeOffset.UtcNow, 2);
        tournament.Teams.Add(new Team("Team A", tournament.Id));
        tournament.Teams.Add(new Team("Team B", tournament.Id));

        // Act & Assert
        Assert.False(tournament.CanRegisterTeam());
    }
}
