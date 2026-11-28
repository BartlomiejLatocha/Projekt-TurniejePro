using TurniejePro.Api.Mapping;
using TurniejePro.Contracts.Matches;
using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using TurniejePro.Domain.Exceptions;
using TurniejePro.Domain.Scheduling;
using static TurniejePro.Tests.ScheduleServiceTests;

namespace TurniejePro.Tests;

/// <summary>
/// Daty rund (KAN-332): wyliczanie przy generowaniu terminarza i zmiana daty jednej rundy.
/// </summary>
public class RoundDatesTests
{
    private static readonly DateTimeOffset RegistrationEnds = new(2026, 12, 1, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FirstRound = new(2026, 12, 5, 10, 0, 0, TimeSpan.Zero);

    // --- Generowanie ---

    [Fact]
    public async Task League_EachRoundGetsFirstDatePlusWeekPerRound()
    {
        var (tournament, sut, uow) = Setup(TournamentFormat.League, teamCount: 4);

        await sut.GenerateScheduleAsync(tournament.Id, ScheduleSeeding.Registration, new RoundDates(FirstRound));

        foreach (var match in uow.MatchesList)
        {
            Assert.Equal(FirstRound.AddDays((match.Round - 1) * 7), match.ScheduledAt);
        }

        Assert.Equal(3, uow.MatchesList.Select(m => m.ScheduledAt).Distinct().Count());
    }

    [Fact]
    public async Task CustomInterval_IsUsed()
    {
        var (tournament, sut, uow) = Setup(TournamentFormat.League, teamCount: 4);

        await sut.GenerateScheduleAsync(tournament.Id, ScheduleSeeding.Registration, new RoundDates(FirstRound, 3));

        Assert.Equal(FirstRound.AddDays(6), uow.MatchesList.Where(m => m.Round == 3).Select(m => m.ScheduledAt).Distinct().Single());
    }

    [Fact]
    public async Task Cup_AllRoundsIncludingByesAndUnknownTeamsGetDates()
    {
        var (tournament, sut, uow) = Setup(TournamentFormat.Cup, teamCount: 5);

        await sut.GenerateScheduleAsync(tournament.Id, ScheduleSeeding.Registration, new RoundDates(FirstRound));

        Assert.Contains(uow.MatchesList, m => m.IsBye);
        Assert.All(uow.MatchesList, m => Assert.Equal(FirstRound.AddDays((m.Round - 1) * 7), m.ScheduledAt));
    }

    [Fact]
    public async Task GroupsAndCup_GroupStageRoundsGetDates()
    {
        var (tournament, sut, uow) = Setup(TournamentFormat.GroupsAndCup, teamCount: 6);

        await sut.GenerateScheduleAsync(tournament.Id, ScheduleSeeding.Registration, new RoundDates(FirstRound));

        Assert.NotEmpty(uow.MatchesList);
        Assert.All(uow.MatchesList, m => Assert.NotNull(m.ScheduledAt));
    }

    [Fact]
    public async Task WithoutDates_MatchesHaveNoDate()
    {
        var (tournament, sut, uow) = Setup(TournamentFormat.League, teamCount: 4);

        await sut.GenerateScheduleAsync(tournament.Id, ScheduleSeeding.Registration);

        Assert.All(uow.MatchesList, m => Assert.Null(m.ScheduledAt));
    }

    [Fact]
    public async Task FirstRoundBeforeRegistrationEnds_ThrowsAndGeneratesNothing()
    {
        var (tournament, sut, uow) = Setup(TournamentFormat.League, teamCount: 4);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sut.GenerateScheduleAsync(tournament.Id, ScheduleSeeding.Registration, new RoundDates(RegistrationEnds.AddMinutes(-1))));

        Assert.Empty(uow.MatchesList);
        Assert.False(tournament.IsStarted);
    }

    [Fact]
    public async Task FirstRoundExactlyWhenRegistrationEnds_IsAllowed()
    {
        var (tournament, sut, uow) = Setup(TournamentFormat.League, teamCount: 2);

        await sut.GenerateScheduleAsync(tournament.Id, ScheduleSeeding.Registration, new RoundDates(RegistrationEnds));

        Assert.Equal(RegistrationEnds, Assert.Single(uow.MatchesList).ScheduledAt);
    }

    [Fact]
    public void RoundDates_KeepPolishWallClockHour_AcrossSpringTimeChange()
    {
        // Sobota 13.03.2027, 10:00 czasu polskiego (zima, UTC+1); czas letni od 28.03.2027
        var dates = new RoundDates(new DateTimeOffset(2027, 3, 13, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateTimeOffset(2027, 3, 27, 9, 0, 0, TimeSpan.Zero), dates.For(3)); // 10:00 zimą
        Assert.Equal(new DateTimeOffset(2027, 4, 3, 8, 0, 0, TimeSpan.Zero), dates.For(4));  // 10:00 latem (UTC+2)
        Assert.All(Enumerable.Range(1, 6), round => Assert.Equal(10, PolishTime.ToPolish(dates.For(round)).Hour));
    }

    [Fact]
    public void RoundDates_KeepPolishWallClockHour_AcrossAutumnTimeChange()
    {
        // Sobota 23.10.2027, 18:30 czasu polskiego (lato, UTC+2); czas zimowy od 31.10.2027
        var dates = new RoundDates(new DateTimeOffset(2027, 10, 23, 16, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateTimeOffset(2027, 10, 30, 16, 30, 0, TimeSpan.Zero), dates.For(2));
        Assert.Equal(new DateTimeOffset(2027, 11, 6, 17, 30, 0, TimeSpan.Zero), dates.For(3));
        Assert.All(Enumerable.Range(1, 4), round =>
        {
            var local = PolishTime.ToPolish(dates.For(round));
            Assert.Equal((18, 30, DayOfWeek.Saturday), (local.Hour, local.Minute, local.DayOfWeek));
        });
    }

    [Fact]
    public void RoundDates_ResultIsUtc()
    {
        var dates = new RoundDates(new DateTimeOffset(2026, 12, 5, 11, 0, 0, TimeSpan.FromHours(1)));

        Assert.Equal(TimeSpan.Zero, dates.For(2).Offset);
        Assert.Equal(new DateTimeOffset(2026, 12, 12, 10, 0, 0, TimeSpan.Zero), dates.For(2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(61)]
    public void RoundDates_IntervalOutOfRange_Throws(int days)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RoundDates(FirstRound, days));
    }

    [Fact]
    public void RoundDates_RoundBelowOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RoundDates(FirstRound).For(0));
    }

    // --- Zmiana daty rundy ---

    [Fact]
    public async Task SetRoundDate_ChangesOnlyThatRound()
    {
        var (tournament, sut, uow) = await GeneratedLeagueAsync();
        var newDate = FirstRound.AddDays(30);

        var changed = await sut.SetRoundDateAsync(tournament.Id, 2, newDate);

        Assert.All(changed, m => Assert.Equal(newDate, m.ScheduledAt));
        Assert.All(uow.MatchesList.Where(m => m.Round == 2), m => Assert.Equal(newDate, m.ScheduledAt));
        Assert.All(uow.MatchesList.Where(m => m.Round == 1), m => Assert.Equal(FirstRound, m.ScheduledAt));
        Assert.Equal(changed.Select(m => m.Position).Order(), changed.Select(m => m.Position));
    }

    [Fact]
    public async Task SetRoundDate_Null_ClearsDate()
    {
        var (tournament, sut, uow) = await GeneratedLeagueAsync();

        await sut.SetRoundDateAsync(tournament.Id, 1, null);

        Assert.All(uow.MatchesList.Where(m => m.Round == 1), m => Assert.Null(m.ScheduledAt));
    }

    [Fact]
    public async Task SetRoundDate_UnknownRoundOrTournament_ThrowsNotFound()
    {
        var (tournament, sut, _) = await GeneratedLeagueAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.SetRoundDateAsync(tournament.Id, 99, FirstRound));
        await Assert.ThrowsAsync<NotFoundException>(() => sut.SetRoundDateAsync(Guid.NewGuid(), 1, FirstRound));
    }

    [Fact]
    public async Task SetRoundDate_RoundWithResult_ThrowsConflictButOtherRoundsWork()
    {
        var (tournament, sut, uow) = await GeneratedLeagueAsync();
        uow.MatchesList.First(m => m.Round == 1).SetResult(2, 1);

        await Assert.ThrowsAsync<ConflictException>(() => sut.SetRoundDateAsync(tournament.Id, 1, FirstRound.AddDays(1)));
        await sut.SetRoundDateAsync(tournament.Id, 2, FirstRound.AddDays(20));

        Assert.All(uow.MatchesList.Where(m => m.Round == 1), m => Assert.Equal(FirstRound, m.ScheduledAt));
    }

    [Fact]
    public async Task SetRoundDate_RoundWithOnlyByes_IsNotTreatedAsPlayed()
    {
        var (tournament, sut, uow) = Setup(TournamentFormat.Cup, teamCount: 5);
        await sut.GenerateScheduleAsync(tournament.Id, ScheduleSeeding.Registration, new RoundDates(FirstRound));

        await sut.SetRoundDateAsync(tournament.Id, 1, FirstRound.AddDays(2));

        Assert.All(uow.MatchesList.Where(m => m.Round == 1), m => Assert.Equal(FirstRound.AddDays(2), m.ScheduledAt));
    }

    [Fact]
    public async Task SetRoundDate_BeforeRegistrationEnds_Throws()
    {
        var (tournament, sut, _) = await GeneratedLeagueAsync();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => sut.SetRoundDateAsync(tournament.Id, 1, RegistrationEnds.AddDays(-1)));

        // Koniec zapisów 1.12.2026 18:00 UTC to 19:00 czasu polskiego (zima, UTC+1)
        Assert.Contains("01.12.2026 19:00", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetRoundDate_InFinishedTournament_OnlyAdmin()
    {
        var (tournament, sut, uow) = Setup(TournamentFormat.League, teamCount: 2);
        await sut.GenerateScheduleAsync(tournament.Id, ScheduleSeeding.Registration);
        var match = Assert.Single(uow.MatchesList);
        match.SetResult(1, 0);
        tournament.CompleteIfAllMatchesFinished(uow.MatchesList);
        match.ScheduledAt = null;

        // Zakończony turniej blokuje organizatora; admin przechodzi tę regułę, ale runda ma wynik → 409
        await Assert.ThrowsAsync<BusinessRuleException>(() => sut.SetRoundDateAsync(tournament.Id, 1, FirstRound));
        await Assert.ThrowsAsync<ConflictException>(() => sut.SetRoundDateAsync(tournament.Id, 1, FirstRound, asAdmin: true));
    }

    // --- Mapowanie ---

    [Fact]
    public void RoundResponse_DateIsCommonDateOrNull()
    {
        var tournamentId = Guid.NewGuid();
        var first = new Match(tournamentId, 1, 1, Guid.NewGuid(), Guid.NewGuid(), FirstRound);
        var second = new Match(tournamentId, 1, 2, Guid.NewGuid(), Guid.NewGuid(), FirstRound);
        var undated = new Match(tournamentId, 2, 1, Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(FirstRound, new[] { second, first }.ToRoundResponse(1).Date);
        Assert.Null(new[] { undated }.ToRoundResponse(2).Date);
        Assert.Equal([1, 2], new[] { second, first }.ToRoundResponse(1).Matches.Select(m => m.Position));
    }

    [Fact]
    public void RequestMapping_DefaultsToWeeklyAndNoDates()
    {
        Assert.Null(((GenerateScheduleRequest?)null).ToRoundDates());
        Assert.Null(new GenerateScheduleRequest(null, null).ToRoundDates());
        Assert.Equal(7, new GenerateScheduleRequest(null, null, FirstRound).ToRoundDates()!.DaysBetweenRounds);
        Assert.Equal(14, new GenerateScheduleRequest(null, null, FirstRound, 14).ToRoundDates()!.DaysBetweenRounds);
    }

    // --- Pomocnicze ---

    private static (Tournament Tournament, ScheduleService Sut, FakeUnitOfWork Uow) Setup(TournamentFormat format, int teamCount)
    {
        var tournament = new Tournament("Turniej", "football", format, RegistrationEnds, 16, null);
        var uow = new FakeUnitOfWork(tournament, CreateTeams(tournament.Id, teamCount));
        return (tournament, new ScheduleService(uow), uow);
    }

    private static async Task<(Tournament Tournament, ScheduleService Sut, FakeUnitOfWork Uow)> GeneratedLeagueAsync()
    {
        var setup = Setup(TournamentFormat.League, teamCount: 4);
        await setup.Sut.GenerateScheduleAsync(setup.Tournament.Id, ScheduleSeeding.Registration, new RoundDates(FirstRound));
        return setup;
    }
}
