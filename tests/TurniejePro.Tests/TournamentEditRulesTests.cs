using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using TurniejePro.Domain.Exceptions;

namespace TurniejePro.Tests;

/// <summary>
/// Co wolno zmienić w turnieju zależnie od statusu (KAN-290): Szkic – wszystko; Zapisy – nazwę, termin i limit;
/// W trakcie – nazwę; Zakończony – nic. Administrator pomija ograniczenia statusu, ale nie reguły spójności danych.
/// </summary>
public class TournamentEditRulesTests
{
    private static readonly DateTimeOffset InTwoWeeks = DateTimeOffset.UtcNow.AddDays(14);

    public enum Field
    {
        Name,
        Discipline,
        Format,
        StartsAt,
        MaxTeams
    }

    // Kolumny: status, zmieniane pole, czy organizator może to zmienić
    public static TheoryData<TournamentStatus, Field, bool> OrganizerMatrix => new()
    {
        { TournamentStatus.Draft, Field.Name, true },
        { TournamentStatus.Draft, Field.Discipline, true },
        { TournamentStatus.Draft, Field.Format, true },
        { TournamentStatus.Draft, Field.StartsAt, true },
        { TournamentStatus.Draft, Field.MaxTeams, true },

        { TournamentStatus.RegistrationOpen, Field.Name, true },
        { TournamentStatus.RegistrationOpen, Field.Discipline, false },
        { TournamentStatus.RegistrationOpen, Field.Format, false },
        { TournamentStatus.RegistrationOpen, Field.StartsAt, true },
        { TournamentStatus.RegistrationOpen, Field.MaxTeams, true },

        { TournamentStatus.InProgress, Field.Name, true },
        { TournamentStatus.InProgress, Field.Discipline, false },
        { TournamentStatus.InProgress, Field.Format, false },
        { TournamentStatus.InProgress, Field.StartsAt, false },
        { TournamentStatus.InProgress, Field.MaxTeams, false },

        { TournamentStatus.Finished, Field.Name, false },
        { TournamentStatus.Finished, Field.Discipline, false },
        { TournamentStatus.Finished, Field.Format, false },
        { TournamentStatus.Finished, Field.StartsAt, false },
        { TournamentStatus.Finished, Field.MaxTeams, false }
    };

    [Theory]
    [MemberData(nameof(OrganizerMatrix))]
    public void Organizer_CanChangeOnlyFieldsAllowedInStatus(TournamentStatus status, Field field, bool allowed)
    {
        var tournament = InStatus(status, withSchedule: false);
        var before = Snapshot(tournament);

        var change = () => Change(tournament, field, asAdmin: false);

        if (allowed)
        {
            change();
            Assert.NotEqual(before, Snapshot(tournament));
        }
        else
        {
            Assert.Throws<BusinessRuleException>(change);
            Assert.Equal(before, Snapshot(tournament));
        }
    }

    [Theory]
    [InlineData(TournamentStatus.RegistrationOpen, Field.Discipline)]
    [InlineData(TournamentStatus.InProgress, Field.MaxTeams)]
    [InlineData(TournamentStatus.Finished, Field.Name)]
    [InlineData(TournamentStatus.InProgress, Field.StartsAt)]
    public void Admin_BypassesStatusRules(TournamentStatus status, Field field)
    {
        var tournament = InStatus(status, withSchedule: false);
        var before = Snapshot(tournament);

        Change(tournament, field, asAdmin: true);

        Assert.NotEqual(before, Snapshot(tournament));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FormatChange_WhenScheduleExists_IsBlockedEvenForAdmin(bool asAdmin)
    {
        var tournament = InStatus(TournamentStatus.InProgress, withSchedule: true);

        var exception = Assert.Throws<BusinessRuleException>(() => Change(tournament, Field.Format, asAdmin));

        Assert.Equal(TournamentFormat.League, tournament.Format);
        if (asAdmin)
        {
            Assert.Contains("terminarz", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Admin_CannotLowerLimitBelowRegisteredTeams()
    {
        var tournament = InStatus(TournamentStatus.InProgress, withSchedule: false);
        tournament.Teams.Add(new Team("A", tournament.Id));
        tournament.Teams.Add(new Team("B", tournament.Id));
        tournament.Teams.Add(new Team("C", tournament.Id));

        Assert.Throws<BusinessRuleException>(() =>
            tournament.UpdateDetails(tournament.Name, tournament.Discipline, tournament.Format, tournament.StartsAt, 2, asAdmin: true));
    }

    [Theory]
    [InlineData(TournamentStatus.RegistrationOpen)]
    [InlineData(TournamentStatus.InProgress)]
    [InlineData(TournamentStatus.Finished)]
    public void SendingUnchangedValues_IsNotAChange(TournamentStatus status)
    {
        var tournament = InStatus(status, withSchedule: false);

        // Formularz odsyła ten sam termin z inną precyzją i dyscyplinę z inną wielkością liter
        tournament.UpdateDetails(
            tournament.Name,
            tournament.Discipline.ToUpperInvariant(),
            tournament.Format,
            tournament.StartsAt.AddSeconds(20),
            tournament.MaxTeams);

        Assert.Equal(status, tournament.Status);
    }

    [Fact]
    public void BlockedChange_MessageNamesAllowedAndBlockedFields()
    {
        var tournament = InStatus(TournamentStatus.RegistrationOpen, withSchedule: false);

        var exception = Assert.Throws<BusinessRuleException>(() =>
            tournament.UpdateDetails("Nowa nazwa", "chess", TournamentFormat.Cup, tournament.StartsAt, tournament.MaxTeams));

        Assert.Contains("„Zapisy”", exception.Message, StringComparison.Ordinal);
        Assert.Contains("nazwa, termin rozpoczęcia, limit drużyn", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Zablokowane zmiany: dyscyplina, format", exception.Message, StringComparison.Ordinal);
        Assert.Equal("Turniej", tournament.Name);
    }

    [Fact]
    public void FinishedTournament_MessageSaysItCannotBeEdited()
    {
        var tournament = InStatus(TournamentStatus.Finished, withSchedule: false);

        var exception = Assert.Throws<BusinessRuleException>(() => Change(tournament, Field.Name, asAdmin: false));

        Assert.StartsWith("Zakończonego turnieju nie można edytować.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StartsAtChangedToPast_ThrowsArgumentException()
    {
        var tournament = InStatus(TournamentStatus.Draft, withSchedule: false);

        Assert.Throws<ArgumentException>(() =>
            tournament.UpdateDetails(tournament.Name, tournament.Discipline, tournament.Format, DateTimeOffset.UtcNow.AddDays(-1), tournament.MaxTeams));
    }

    [Fact]
    public void RenamingStartedTournament_WithStartDateInPast_Works()
    {
        var tournament = new Tournament("Turniej", "football", TournamentFormat.League, DateTimeOffset.UtcNow.AddDays(-3), 8, null);
        tournament.Start();

        tournament.UpdateDetails("Nowa nazwa", "football", TournamentFormat.League, tournament.StartsAt, 8);

        Assert.Equal("Nowa nazwa", tournament.Name);
    }

    // --- Punktacja ---

    [Fact]
    public void ScoringRules_InDraft_CanBeChangedByOrganizer()
    {
        var tournament = InStatus(TournamentStatus.Draft, withSchedule: false);

        tournament.UpdateScoringRules(new ScoringRules(2, 0, 1, false));

        Assert.Equal(new ScoringRules(2, 0, 1, false), tournament.ScoringRules);
    }

    [Theory]
    [InlineData(TournamentStatus.RegistrationOpen)]
    [InlineData(TournamentStatus.InProgress)]
    [InlineData(TournamentStatus.Finished)]
    public void ScoringRules_AfterDraft_BlockedForOrganizerButNotForAdmin(TournamentStatus status)
    {
        var tournament = InStatus(status, withSchedule: false);
        var newRules = new ScoringRules(2, 0, 1, false);

        Assert.Throws<BusinessRuleException>(() => tournament.UpdateScoringRules(newRules));
        tournament.UpdateScoringRules(newRules, asAdmin: true);

        Assert.Equal(newRules, tournament.ScoringRules);
    }

    [Fact]
    public void ScoringRules_SameAsCurrent_IsNotAChange()
    {
        var tournament = InStatus(TournamentStatus.Finished, withSchedule: false);

        tournament.UpdateScoringRules(tournament.ScoringRules);

        Assert.Equal(TournamentStatus.Finished, tournament.Status);
    }

    [Fact]
    public void UpdateDetails_WithNewScoringRules_InRegistration_IsBlocked()
    {
        var tournament = InStatus(TournamentStatus.RegistrationOpen, withSchedule: false);

        Assert.Throws<BusinessRuleException>(() => tournament.UpdateDetails(
            tournament.Name, tournament.Discipline, tournament.Format, tournament.StartsAt, tournament.MaxTeams, new ScoringRules(2, 0, 1, false)));
    }

    // --- Pomocnicze ---

    private static Tournament InStatus(TournamentStatus status, bool withSchedule)
    {
        var tournament = new Tournament("Turniej", "football", TournamentFormat.League, InTwoWeeks, 8, null);
        var match = new Match(tournament.Id, 1, 1, Guid.NewGuid(), Guid.NewGuid());
        if (withSchedule || status == TournamentStatus.Finished)
        {
            tournament.Matches.Add(match);
        }

        switch (status)
        {
            case TournamentStatus.RegistrationOpen:
                tournament.OpenRegistration();
                break;
            case TournamentStatus.InProgress:
                tournament.Start();
                break;
            case TournamentStatus.Finished:
                tournament.Start();
                match.SetResult(1, 0);
                tournament.CompleteIfAllMatchesFinished([match]);
                if (!withSchedule)
                {
                    tournament.Matches.Clear();
                }

                break;
        }

        Assert.Equal(status, tournament.Status);
        return tournament;
    }

    private static void Change(Tournament t, Field field, bool asAdmin) => t.UpdateDetails(
        field == Field.Name ? "Zmieniona nazwa" : t.Name,
        field == Field.Discipline ? "chess" : t.Discipline,
        field == Field.Format ? TournamentFormat.Cup : t.Format,
        field == Field.StartsAt ? t.StartsAt.AddDays(3) : t.StartsAt,
        field == Field.MaxTeams ? t.MaxTeams + 4 : t.MaxTeams,
        asAdmin: asAdmin);

    private static (string, string, TournamentFormat, DateTimeOffset, int) Snapshot(Tournament t) =>
        (t.Name, t.Discipline, t.Format, t.StartsAt, t.MaxTeams);
}
