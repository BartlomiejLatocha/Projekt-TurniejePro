using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using TurniejePro.Domain.Exceptions;

namespace TurniejePro.Tests;

/// <summary>
/// Limit drużyn liczony bez odrzuconych zgłoszeń (KAN-280): do limitu liczą się zgłoszenia oczekujące i zaakceptowane,
/// a odrzucone zwalnia miejsce.
/// </summary>
public class TournamentTeamLimitTests
{
    private static readonly DateTimeOffset StartsAt = new(2030, 6, 1, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ActiveTeamsCount_CountsPendingAndAcceptedButNotRejected()
    {
        var tournament = CreateTournament(maxTeams: 8);
        AddTeam(tournament, TeamRegistrationStatus.Pending);
        AddTeam(tournament, TeamRegistrationStatus.Accepted);
        AddTeam(tournament, TeamRegistrationStatus.Rejected);
        AddTeam(tournament, TeamRegistrationStatus.Rejected);

        Assert.Equal(2, tournament.ActiveTeamsCount);
    }

    [Fact]
    public void CanRegisterTeam_RejectedTeamsDoNotOccupySlots()
    {
        var tournament = CreateTournament(maxTeams: 2);
        AddTeam(tournament, TeamRegistrationStatus.Pending);
        AddTeam(tournament, TeamRegistrationStatus.Rejected);
        AddTeam(tournament, TeamRegistrationStatus.Rejected);

        Assert.True(tournament.CanRegisterTeam());
    }

    [Fact]
    public void CanRegisterTeam_PendingAndAcceptedTeamsOccupySlots()
    {
        var tournament = CreateTournament(maxTeams: 2);
        AddTeam(tournament, TeamRegistrationStatus.Accepted);
        AddTeam(tournament, TeamRegistrationStatus.Pending);

        Assert.False(tournament.CanRegisterTeam());
    }

    [Fact]
    public void CanRegisterTeam_RejectingATeamInFullTournament_FreesTheSlot()
    {
        var tournament = CreateTournament(maxTeams: 2);
        AddTeam(tournament, TeamRegistrationStatus.Pending);
        var second = AddTeam(tournament, TeamRegistrationStatus.Pending);
        Assert.False(tournament.CanRegisterTeam());

        second.Reject();

        Assert.True(tournament.CanRegisterTeam());
    }

    [Fact]
    public void CanRegisterTeam_OnlyRejectedTeamsEvenAboveTheLimit_StaysOpen()
    {
        var tournament = CreateTournament(maxTeams: 2);
        for (var i = 0; i < 5; i++)
        {
            AddTeam(tournament, TeamRegistrationStatus.Rejected);
        }

        Assert.True(tournament.CanRegisterTeam());
    }

    [Fact]
    public void GetRegistrationBlockReason_LimitFilledOnlyByRejectedTeams_ReturnsNull()
    {
        var tournament = CreateTournament(maxTeams: 2);
        AddTeam(tournament, TeamRegistrationStatus.Rejected);
        AddTeam(tournament, TeamRegistrationStatus.Rejected);

        Assert.Null(tournament.GetRegistrationBlockReason(StartsAt.AddDays(-1)));
    }

    [Fact]
    public void GetRegistrationBlockReason_LimitFilledByActiveTeams_ReturnsLimitReached()
    {
        var tournament = CreateTournament(maxTeams: 2);
        AddTeam(tournament, TeamRegistrationStatus.Accepted);
        AddTeam(tournament, TeamRegistrationStatus.Accepted);
        AddTeam(tournament, TeamRegistrationStatus.Rejected);

        Assert.Equal(RegistrationBlockReason.LimitReached, tournament.GetRegistrationBlockReason(StartsAt.AddDays(-1)));
    }

    [Fact]
    public void UpdateDetails_LimitBelowRejectedTeamsButNotBelowActiveOnes_Succeeds()
    {
        var tournament = CreateTournament(maxTeams: 8);
        AddTeam(tournament, TeamRegistrationStatus.Pending);
        AddTeam(tournament, TeamRegistrationStatus.Rejected);
        AddTeam(tournament, TeamRegistrationStatus.Rejected);
        AddTeam(tournament, TeamRegistrationStatus.Rejected);

        tournament.UpdateDetails("Liga", "Piłka nożna", TournamentFormat.League, StartsAt, maxTeams: 2);

        Assert.Equal(2, tournament.MaxTeams);
    }

    [Fact]
    public void UpdateDetails_LimitEqualToNumberOfActiveTeams_Succeeds()
    {
        var tournament = CreateTournament(maxTeams: 8);
        AddTeam(tournament, TeamRegistrationStatus.Accepted);
        AddTeam(tournament, TeamRegistrationStatus.Pending);
        AddTeam(tournament, TeamRegistrationStatus.Rejected);

        tournament.UpdateDetails("Liga", "Piłka nożna", TournamentFormat.League, StartsAt, maxTeams: 2);

        Assert.Equal(2, tournament.MaxTeams);
    }

    [Fact]
    public void UpdateDetails_LimitBelowNumberOfActiveTeams_ThrowsBusinessRuleException()
    {
        var tournament = CreateTournament(maxTeams: 8);
        AddTeam(tournament, TeamRegistrationStatus.Accepted);
        AddTeam(tournament, TeamRegistrationStatus.Accepted);
        AddTeam(tournament, TeamRegistrationStatus.Pending);
        AddTeam(tournament, TeamRegistrationStatus.Rejected);

        Assert.Throws<BusinessRuleException>(() =>
            tournament.UpdateDetails("Liga", "Piłka nożna", TournamentFormat.League, StartsAt, maxTeams: 2));
    }

    private static Tournament CreateTournament(int maxTeams) =>
        new("Liga Szkolna", "Piłka nożna", TournamentFormat.League, StartsAt, maxTeams);

    private static Team AddTeam(Tournament tournament, TeamRegistrationStatus status)
    {
        var team = new Team($"Drużyna {tournament.Teams.Count + 1}", tournament.Id);

        switch (status)
        {
            case TeamRegistrationStatus.Accepted:
                team.Accept();
                break;
            case TeamRegistrationStatus.Rejected:
                team.Reject();
                break;
        }

        tournament.Teams.Add(team);

        return team;
    }
}
