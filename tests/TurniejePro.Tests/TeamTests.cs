using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using Xunit;

namespace TurniejePro.Tests;

public class TeamTests
{
    [Fact]
    public void Constructor_ValidArguments_ShouldSetProperties()
    {
        var tournamentId = Guid.NewGuid();
        var captainId = Guid.NewGuid();

        var team = new Team("Orły", tournamentId, captainId, "kontakt@orly.pl");

        Assert.Equal("Orły", team.Name);
        Assert.Equal(tournamentId, team.TournamentId);
        Assert.Equal(captainId, team.CaptainId);
        Assert.Equal("kontakt@orly.pl", team.ContactEmail);
        Assert.False(team.IsAccepted);
        Assert.Equal(TeamRegistrationStatus.Pending, team.Status);
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
    public void Accept_ShouldSetIsAcceptedToTrueAndStatusToAccepted()
    {
        var team = new Team("Orły", Guid.NewGuid());

        team.Accept();

        Assert.True(team.IsAccepted);
        Assert.Equal(TeamRegistrationStatus.Accepted, team.Status);
    }

    [Fact]
    public void Reject_ShouldSetIsAcceptedToFalseAndStatusToRejected()
    {
        var team = new Team("Orły", Guid.NewGuid());
        team.Accept();

        team.Reject();

        Assert.False(team.IsAccepted);
        Assert.Equal(TeamRegistrationStatus.Rejected, team.Status);
    }

    [Fact]
    public void ResetToPending_ShouldSetStatusToPending()
    {
        var team = new Team("Orły", Guid.NewGuid());
        team.Accept();

        team.ResetToPending();

        Assert.False(team.IsAccepted);
        Assert.Equal(TeamRegistrationStatus.Pending, team.Status);
    }

    [Fact]
    public void AddPlayer_ShouldAppendPlayerToCollection()
    {
        var team = new Team("Orły", Guid.NewGuid());

        var player = team.AddPlayer("Jan Kowalski", 10);

        Assert.Single(team.Players);
        Assert.Contains(player, team.Players);
        Assert.Equal("Jan Kowalski", player.FullName);
        Assert.Equal(10, player.JerseyNumber);
        Assert.Equal(team.Id, player.TeamId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AddPlayer_EmptyName_ShouldThrowArgumentException(string? invalidName)
    {
        var team = new Team("Orły", Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => team.AddPlayer(invalidName!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100)]
    public void AddPlayer_InvalidJerseyNumber_ShouldThrowArgumentOutOfRangeException(int invalidNumber)
    {
        var team = new Team("Orły", Guid.NewGuid());

        Assert.Throws<ArgumentOutOfRangeException>(() => team.AddPlayer("Jan Kowalski", invalidNumber));
    }

    [Fact]
    public void AddPlayer_DuplicateJerseyNumber_ShouldThrowInvalidOperationException()
    {
        var team = new Team("Orły", Guid.NewGuid());
        team.AddPlayer("Jan Kowalski", 10);

        Assert.Throws<InvalidOperationException>(() => team.AddPlayer("Adam Nowak", 10));
    }

    [Fact]
    public void RemovePlayer_ExistingPlayer_ShouldReturnTrueAndRemoveFromCollection()
    {
        var team = new Team("Orły", Guid.NewGuid());
        var player = team.AddPlayer("Jan Kowalski", 10);

        var removed = team.RemovePlayer(player.Id);

        Assert.True(removed);
        Assert.Empty(team.Players);
    }

    [Fact]
    public void RemovePlayer_NonExistingPlayer_ShouldReturnFalse()
    {
        var team = new Team("Orły", Guid.NewGuid());

        var removed = team.RemovePlayer(Guid.NewGuid());

        Assert.False(removed);
    }

    [Fact]
    public void UpdateDetails_ValidArguments_ShouldUpdateNameAndContactEmail()
    {
        var team = new Team("Stara Nazwa", Guid.NewGuid(), null, "stary@email.com");

        team.UpdateDetails("Nowa Nazwa", "nowy@email.com");

        Assert.Equal("Nowa Nazwa", team.Name);
        Assert.Equal("nowy@email.com", team.ContactEmail);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UpdateDetails_EmptyName_ShouldThrowArgumentException(string? invalidName)
    {
        var team = new Team("Orły", Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => team.UpdateDetails(invalidName!, null));
    }
}
