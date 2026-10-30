using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using Xunit;

namespace TurniejePro.Tests;

public class TournamentTests
{
    [Fact]
    public void TournamentShouldInitializeWithCorrectNameAndStatus()
    {
        var tournamentName = "Mistrzostwa Polski w eSporcie";

        var tournament = new Tournament(tournamentName);

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
        var organizerId = Guid.NewGuid();
        var startsAt = DateTimeOffset.UtcNow.AddDays(7);

        var tournament = new Tournament(
            "Liga Szkolna",
            "Piłka Nożna",
            TournamentFormat.League,
            startsAt,
            8,
            organizerId);

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
        var tournament = new Tournament("Turniej CS");

        tournament.Start();

        Assert.True(tournament.IsStarted);
    }

    [Fact]
    public void Start_WhenAlreadyStarted_ShouldThrowInvalidOperationException()
    {
        var tournament = new Tournament("Turniej CS");
        tournament.Start();

        var ex = Assert.Throws<InvalidOperationException>(() => tournament.Start());
        Assert.Equal("Turniej został już rozpoczęty.", ex.Message);
    }

    [Fact]
    public void CanRegisterTeam_WhenNotStartedAndBelowLimit_ShouldReturnTrue()
    {
        var tournament = new Tournament("Turniej CS");

        Assert.True(tournament.CanRegisterTeam());
    }

    [Fact]
    public void CanRegisterTeam_WhenStarted_ShouldReturnFalse()
    {
        var tournament = new Tournament("Turniej CS");
        tournament.Start();

        Assert.False(tournament.CanRegisterTeam());
    }

    [Fact]
    public void CanRegisterTeam_WhenMaxTeamsReached_ShouldReturnFalse()
    {
        var tournament = new Tournament("Turniej 2 Drużyn", "FIFA", TournamentFormat.Cup, DateTimeOffset.UtcNow, 2);
        tournament.Teams.Add(new Team("Team A", tournament.Id));
        tournament.Teams.Add(new Team("Team B", tournament.Id));

        Assert.False(tournament.CanRegisterTeam());
    }

    [Fact]
    public void UpdateDetails_ValidArguments_ShouldUpdateProperties()
    {
        var tournament = new Tournament("Stara Nazwa", "FIFA", TournamentFormat.Cup, DateTimeOffset.UtcNow, 8);
        var newStartsAt = DateTimeOffset.UtcNow.AddDays(10);

        tournament.UpdateDetails("Nowa Nazwa", "Koszykówka", TournamentFormat.League, newStartsAt, 16);

        Assert.Equal("Nowa Nazwa", tournament.Name);
        Assert.Equal("Koszykówka", tournament.Discipline);
        Assert.Equal(TournamentFormat.League, tournament.Format);
        Assert.Equal(newStartsAt, tournament.StartsAt);
        Assert.Equal(16, tournament.MaxTeams);
    }

    [Fact]
    public void UpdateDetails_WhenStarted_ShouldThrowInvalidOperationException()
    {
        var tournament = new Tournament("Turniej", "FIFA", TournamentFormat.Cup, DateTimeOffset.UtcNow, 8);
        tournament.Start();

        Assert.Throws<InvalidOperationException>(() =>
            tournament.UpdateDetails("Nowa", "FIFA", TournamentFormat.Cup, DateTimeOffset.UtcNow, 8));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UpdateDetails_InvalidName_ShouldThrowArgumentException(string? invalidName)
    {
        var tournament = new Tournament("Turniej", "FIFA", TournamentFormat.Cup, DateTimeOffset.UtcNow, 8);

        Assert.Throws<ArgumentException>(() =>
            tournament.UpdateDetails(invalidName!, "FIFA", TournamentFormat.Cup, DateTimeOffset.UtcNow, 8));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UpdateDetails_InvalidDiscipline_ShouldThrowArgumentException(string? invalidDiscipline)
    {
        var tournament = new Tournament("Turniej", "FIFA", TournamentFormat.Cup, DateTimeOffset.UtcNow, 8);

        Assert.Throws<ArgumentException>(() =>
            tournament.UpdateDetails("Turniej", invalidDiscipline!, TournamentFormat.Cup, DateTimeOffset.UtcNow, 8));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-10)]
    public void UpdateDetails_MaxTeamsLessThanTwo_ShouldThrowArgumentOutOfRangeException(int invalidMaxTeams)
    {
        var tournament = new Tournament("Turniej", "FIFA", TournamentFormat.Cup, DateTimeOffset.UtcNow, 8);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            tournament.UpdateDetails("Turniej", "FIFA", TournamentFormat.Cup, DateTimeOffset.UtcNow, invalidMaxTeams));
    }

    [Fact]
    public void UpdateDetails_MaxTeamsLessThanRegisteredTeams_ShouldThrowInvalidOperationException()
    {
        var tournament = new Tournament("Turniej", "FIFA", TournamentFormat.Cup, DateTimeOffset.UtcNow, 8);
        tournament.Teams.Add(new Team("Drużyna 1", tournament.Id));
        tournament.Teams.Add(new Team("Drużyna 2", tournament.Id));
        tournament.Teams.Add(new Team("Drużyna 3", tournament.Id));

        Assert.Throws<InvalidOperationException>(() =>
            tournament.UpdateDetails("Turniej", "FIFA", TournamentFormat.Cup, DateTimeOffset.UtcNow, 2));
    }
}
