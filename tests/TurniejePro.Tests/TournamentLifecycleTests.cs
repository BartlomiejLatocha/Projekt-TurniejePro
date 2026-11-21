using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using TurniejePro.Domain.Exceptions;
using TurniejePro.Domain.Scheduling;
using static TurniejePro.Tests.ScheduleServiceTests;

namespace TurniejePro.Tests;

/// <summary>
/// Cykl życia turnieju powiązany z terminarzem (KAN-290): terminarz rozpoczyna turniej, ręczny start wymaga terminarza,
/// wyniki tylko w rozpoczętym turnieju, terminarz z wynikami nie da się usunąć, a usunięcie cofa turniej do zapisów.
/// </summary>
public class TournamentLifecycleTests
{
    // --- Generowanie terminarza ---

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenerateSchedule_StartsTournament_FromDraftOrRegistration(bool openRegistrationFirst)
    {
        var (tournament, sut, _) = Setup(TournamentFormat.League, teamCount: 4);
        if (openRegistrationFirst)
        {
            tournament.OpenRegistration();
        }

        await sut.GenerateScheduleAsync(tournament.Id);

        Assert.Equal(TournamentStatus.InProgress, tournament.Status);
        Assert.True(tournament.IsStarted);
    }

    [Fact]
    public async Task GenerateSchedule_ForTournamentStartedEarlier_KeepsItInProgress()
    {
        var (tournament, sut, _) = Setup(TournamentFormat.Cup, teamCount: 4);
        tournament.Start();

        await sut.GenerateScheduleAsync(tournament.Id);

        Assert.Equal(TournamentStatus.InProgress, tournament.Status);
    }

    [Fact]
    public async Task GenerateSchedule_WithTooFewTeams_DoesNotStartTournament()
    {
        var (tournament, sut, _) = Setup(TournamentFormat.League, teamCount: 1);

        await Assert.ThrowsAsync<BusinessRuleException>(() => sut.GenerateScheduleAsync(tournament.Id));

        Assert.Equal(TournamentStatus.Draft, tournament.Status);
    }

    [Fact]
    public async Task AfterSchedule_FormatChangeAndNewRegistrations_AreBlocked()
    {
        var (tournament, sut, _) = Setup(TournamentFormat.League, teamCount: 4);

        await sut.GenerateScheduleAsync(tournament.Id);

        Assert.Throws<BusinessRuleException>(() => tournament.UpdateDetails(
            tournament.Name, tournament.Discipline, TournamentFormat.Cup, tournament.StartsAt, tournament.MaxTeams));
        Assert.Throws<BusinessRuleException>(() => tournament.UpdateDetails(
            tournament.Name, tournament.Discipline, tournament.Format, tournament.StartsAt, tournament.MaxTeams + 4));
        Assert.Equal(RegistrationBlockReason.TournamentStarted, tournament.GetRegistrationBlockReason(DateTimeOffset.UtcNow));
    }

    // --- Ręczny start ---

    [Fact]
    public void StartWithExistingSchedule_WithoutMatches_ThrowsAndKeepsStatus()
    {
        var tournament = NewTournament(TournamentFormat.Cup);
        tournament.OpenRegistration();

        var exception = Assert.Throws<BusinessRuleException>(tournament.StartWithExistingSchedule);

        Assert.Contains("terminarz", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TournamentStatus.RegistrationOpen, tournament.Status);
    }

    [Fact]
    public void StartWithExistingSchedule_WithMatches_StartsTournament()
    {
        var tournament = NewTournament(TournamentFormat.League);
        tournament.Matches.Add(new Match(tournament.Id, 1, 1, Guid.NewGuid(), Guid.NewGuid()));

        tournament.StartWithExistingSchedule();

        Assert.Equal(TournamentStatus.InProgress, tournament.Status);
    }

    // --- Cofnięcie do zapisów ---

    [Fact]
    public void ReturnToRegistration_FromInProgress_ReopensRegistration()
    {
        var tournament = NewTournament(TournamentFormat.League);
        tournament.Start();

        tournament.ReturnToRegistration();

        Assert.Equal(TournamentStatus.RegistrationOpen, tournament.Status);
        Assert.False(tournament.IsStarted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReturnToRegistration_FromDraftOrRegistration_ChangesNothing(bool openRegistration)
    {
        var tournament = NewTournament(TournamentFormat.League);
        if (openRegistration)
        {
            tournament.OpenRegistration();
        }

        var before = tournament.Status;
        tournament.ReturnToRegistration();

        Assert.Equal(before, tournament.Status);
    }

    [Fact]
    public void ReturnToRegistration_FromFinished_Throws()
    {
        var tournament = NewTournament(TournamentFormat.League);
        tournament.Start();
        var match = new Match(tournament.Id, 1, 1, Guid.NewGuid(), Guid.NewGuid());
        match.SetResult(1, 0);
        Assert.True(tournament.CompleteIfAllMatchesFinished([match]));

        Assert.Throws<BusinessRuleException>(tournament.ReturnToRegistration);
        Assert.Equal(TournamentStatus.Finished, tournament.Status);
    }

    // --- Usuwanie terminarza ---

    [Fact]
    public async Task DeleteSchedule_WithoutResults_RemovesMatchesAndReturnsToRegistration()
    {
        var (tournament, sut, uow) = Setup(TournamentFormat.League, teamCount: 4);
        await sut.GenerateScheduleAsync(tournament.Id);

        await sut.DeleteScheduleAsync(tournament.Id);

        Assert.Empty(uow.MatchesList);
        Assert.Equal(TournamentStatus.RegistrationOpen, tournament.Status);
    }

    [Fact]
    public async Task DeleteSchedule_WithOnlyByesFinished_IsAllowed()
    {
        // Wolne losy są „zakończone” od razu po wygenerowaniu, ale to nie są wyniki wpisane przez organizatora
        var (tournament, sut, uow) = Setup(TournamentFormat.Cup, teamCount: 5);
        await sut.GenerateScheduleAsync(tournament.Id);
        Assert.Contains(uow.MatchesList, m => m.IsBye);

        await sut.DeleteScheduleAsync(tournament.Id);

        Assert.Empty(uow.MatchesList);
    }

    [Fact]
    public async Task DeleteSchedule_WithResult_ThrowsConflictAndKeepsEverything()
    {
        var (tournament, sut, uow) = Setup(TournamentFormat.League, teamCount: 4);
        var matches = await sut.GenerateScheduleAsync(tournament.Id);
        matches[0].SetResult(2, 1);

        await Assert.ThrowsAsync<ConflictException>(() => sut.DeleteScheduleAsync(tournament.Id));

        Assert.Equal(matches.Count, uow.MatchesList.Count);
        Assert.Equal(TournamentStatus.InProgress, tournament.Status);
    }

    [Fact]
    public async Task DeleteSchedule_WhenThereIsNoSchedule_ThrowsNotFound()
    {
        var (tournament, sut, _) = Setup(TournamentFormat.League, teamCount: 4);

        await Assert.ThrowsAsync<NotFoundException>(() => sut.DeleteScheduleAsync(tournament.Id));
    }

    [Fact]
    public async Task DeleteSchedule_OfUnknownTournament_ThrowsNotFound()
    {
        var (_, sut, _) = Setup(TournamentFormat.League, teamCount: 4);

        await Assert.ThrowsAsync<NotFoundException>(() => sut.DeleteScheduleAsync(Guid.NewGuid()));
    }

    // --- Wyniki ---

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SetResult_BeforeStart_ThrowsAndKeepsMatchOpen(bool openRegistration)
    {
        var tournament = NewTournament(TournamentFormat.League);
        if (openRegistration)
        {
            tournament.OpenRegistration();
        }

        var teams = CreateTeams(tournament.Id, 2);
        var uow = new FakeUnitOfWork(tournament, teams);
        var match = new Match(tournament.Id, 1, 1, teams[0].Id, teams[1].Id);
        await uow.Matches.AddAsync(match);

        await Assert.ThrowsAsync<BusinessRuleException>(() => new MatchResultService(uow).SetResultAsync(tournament.Id, match.Id, 2, 1));

        Assert.Null(match.HomeScore);
        Assert.Equal(MatchStatus.Scheduled, match.Status);
    }

    [Fact]
    public async Task SetResult_InFinishedTournament_AllowsCorrection()
    {
        var (tournament, sut, uow) = Setup(TournamentFormat.League, teamCount: 2);
        var matches = await sut.GenerateScheduleAsync(tournament.Id);
        var results = new MatchResultService(uow);
        await results.SetResultAsync(tournament.Id, matches[0].Id, 1, 0);
        Assert.Equal(TournamentStatus.Finished, tournament.Status);

        var corrected = await results.SetResultAsync(tournament.Id, matches[0].Id, 0, 2);

        Assert.Equal(2, corrected.AwayScore);
    }

    // --- Pomocnicze ---

    private static Tournament NewTournament(TournamentFormat format) =>
        new($"Turniej {format}", "Piłka nożna", format, DateTimeOffset.UtcNow.AddDays(7), 8, null);

    private static (Tournament Tournament, ScheduleService Sut, FakeUnitOfWork Uow) Setup(TournamentFormat format, int teamCount)
    {
        var tournament = NewTournament(format);
        var uow = new FakeUnitOfWork(tournament, CreateTeams(tournament.Id, teamCount));
        return (tournament, new ScheduleService(uow), uow);
    }
}
