using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using Xunit;

namespace TurniejePro.Tests;

public class MatchTests
{
    [Fact]
    public void Constructor_ValidArguments_ShouldSetProperties()
    {
        // Arrange
        var tournamentId = Guid.NewGuid();
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();
        var scheduledAt = DateTimeOffset.UtcNow.AddDays(1);

        // Act
        var match = new Match(tournamentId, 1, homeTeamId, awayTeamId, scheduledAt);

        // Assert
        Assert.Equal(tournamentId, match.TournamentId);
        Assert.Equal(1, match.Round);
        Assert.Equal(homeTeamId, match.HomeTeamId);
        Assert.Equal(awayTeamId, match.AwayTeamId);
        Assert.Equal(scheduledAt, match.ScheduledAt);
        Assert.Equal(MatchStatus.Scheduled, match.Status);
        Assert.Null(match.HomeScore);
        Assert.Null(match.AwayScore);
    }

    [Fact]
    public void Constructor_RoundLessThanOne_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Match(Guid.NewGuid(), 0));
    }

    [Fact]
    public void StartMatch_WhenScheduled_ShouldChangeStatusToInProgress()
    {
        // Arrange
        var match = new Match(Guid.NewGuid(), 1);

        // Act
        match.StartMatch();

        // Assert
        Assert.Equal(MatchStatus.InProgress, match.Status);
    }

    [Fact]
    public void StartMatch_WhenNotScheduled_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var match = new Match(Guid.NewGuid(), 1);
        match.StartMatch();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => match.StartMatch());
    }

    [Fact]
    public void SetResult_ValidScores_ShouldSetScoresAndStatusFinished()
    {
        // Arrange
        var match = new Match(Guid.NewGuid(), 1);

        // Act
        match.SetResult(3, 1);

        // Assert
        Assert.Equal(3, match.HomeScore);
        Assert.Equal(1, match.AwayScore);
        Assert.Equal(MatchStatus.Finished, match.Status);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -2)]
    public void SetResult_NegativeScore_ShouldThrowArgumentException(int homeScore, int awayScore)
    {
        var match = new Match(Guid.NewGuid(), 1);
        Assert.Throws<ArgumentException>(() => match.SetResult(homeScore, awayScore));
    }

    [Fact]
    public void Cancel_ShouldChangeStatusToCancelled()
    {
        // Arrange
        var match = new Match(Guid.NewGuid(), 1);

        // Act
        match.Cancel();

        // Assert
        Assert.Equal(MatchStatus.Cancelled, match.Status);
    }
}
