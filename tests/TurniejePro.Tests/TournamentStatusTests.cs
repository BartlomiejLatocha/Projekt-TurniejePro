using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using TurniejePro.Domain.Exceptions;
using Xunit;

namespace TurniejePro.Tests;

public class TournamentStatusTests
{
    private static Tournament CreateTournament(TournamentFormat format = TournamentFormat.League) =>
        new("Test", "football", format, DateTimeOffset.UtcNow.AddDays(10), 8);

    [Fact]
    public void NewTournament_IsDraft_AndNotStarted()
    {
        var tournament = CreateTournament();

        Assert.Equal(TournamentStatus.Draft, tournament.Status);
        Assert.False(tournament.IsStarted);
    }

    [Fact]
    public void OpenRegistration_FromDraft_MovesToRegistrationOpen()
    {
        var tournament = CreateTournament();

        tournament.OpenRegistration();

        Assert.Equal(TournamentStatus.RegistrationOpen, tournament.Status);
        Assert.False(tournament.IsStarted);
    }

    [Fact]
    public void OpenRegistration_TwiceOrAfterStart_Throws()
    {
        var tournament = CreateTournament();
        tournament.OpenRegistration();

        Assert.Throws<BusinessRuleException>(() => tournament.OpenRegistration());

        tournament.Start();
        Assert.Throws<BusinessRuleException>(() => tournament.OpenRegistration());
    }

    [Fact]
    public void Start_FromRegistrationOpen_MovesToInProgress_AndIsStarted()
    {
        var tournament = CreateTournament();
        tournament.OpenRegistration();

        tournament.Start();

        Assert.Equal(TournamentStatus.InProgress, tournament.Status);
        Assert.True(tournament.IsStarted);
    }

    [Fact]
    public void Start_WhenFinished_Throws()
    {
        var tournament = CreateTournament();
        tournament.Start();

        var match = new Match(tournament.Id, 1, 1, Guid.NewGuid(), Guid.NewGuid());
        match.SetResult(1, 0);
        Assert.True(tournament.CompleteIfAllMatchesFinished([match]));

        Assert.Throws<BusinessRuleException>(() => tournament.Start());
    }

    [Fact]
    public void FormatChange_AfterStart_IsBlocked()
    {
        var tournament = CreateTournament(TournamentFormat.League);
        tournament.Start();

        Assert.Throws<BusinessRuleException>(() => tournament.UpdateDetails(
            "Test", "football", TournamentFormat.Cup, DateTimeOffset.UtcNow.AddDays(10), 8));
    }

    [Fact]
    public void CanRegisterTeam_IsFalse_AfterStart()
    {
        var tournament = CreateTournament();
        tournament.OpenRegistration();
        tournament.Start();

        Assert.False(tournament.CanRegisterTeam());
    }

    [Fact]
    public void CompleteIfAllMatchesFinished_NotAllFinished_KeepsInProgress()
    {
        var tournament = CreateTournament();
        tournament.Start();

        var done = new Match(tournament.Id, 1, 1, Guid.NewGuid(), Guid.NewGuid());
        done.SetResult(2, 2);
        var pending = new Match(tournament.Id, 1, 2, Guid.NewGuid(), Guid.NewGuid());

        Assert.False(tournament.CompleteIfAllMatchesFinished([done, pending]));
        Assert.Equal(TournamentStatus.InProgress, tournament.Status);
    }

    [Fact]
    public void CompleteIfAllMatchesFinished_IgnoresCancelledMatches()
    {
        var tournament = CreateTournament();
        tournament.Start();

        var done = new Match(tournament.Id, 1, 1, Guid.NewGuid(), Guid.NewGuid());
        done.SetResult(1, 0);
        var cancelled = new Match(tournament.Id, 1, 2, Guid.NewGuid(), Guid.NewGuid());
        cancelled.Cancel();

        Assert.True(tournament.CompleteIfAllMatchesFinished([done, cancelled]));
        Assert.Equal(TournamentStatus.Finished, tournament.Status);
    }

    [Fact]
    public void CompleteIfAllMatchesFinished_WhenNotInProgress_DoesNothing()
    {
        var tournament = CreateTournament();

        var done = new Match(tournament.Id, 1, 1, Guid.NewGuid(), Guid.NewGuid());
        done.SetResult(1, 0);

        Assert.False(tournament.CompleteIfAllMatchesFinished([done]));
        Assert.Equal(TournamentStatus.Draft, tournament.Status);
    }
}
