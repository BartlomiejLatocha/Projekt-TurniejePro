using TurniejePro.Domain;
using TurniejePro.Domain.Enums;

namespace TurniejePro.Tests;

/// <summary>
/// Testy jednostkowe reguł blokujących zgłoszenia drużyn (KAN-76): start turnieju, upływ terminu i limit drużyn.
/// Chwila sprawdzenia jest podawana jawnie, więc testy nie zależą od zegara.
/// </summary>
public class TournamentRegistrationBlockTests
{
    private static readonly DateTimeOffset StartsAt = new(2030, 6, 1, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GetRegistrationBlockReason_OpenTournament_ReturnsNull()
    {
        var tournament = CreateTournament(maxTeams: 8);

        var reason = tournament.GetRegistrationBlockReason(StartsAt.AddDays(-7));

        Assert.Null(reason);
    }

    [Fact]
    public void GetRegistrationBlockReason_OneSecondBeforeDeadline_ReturnsNull()
    {
        var tournament = CreateTournament(maxTeams: 8);

        var reason = tournament.GetRegistrationBlockReason(StartsAt.AddSeconds(-1));

        Assert.Null(reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3600)]
    [InlineData(31536000)]
    public void GetRegistrationBlockReason_AtOrAfterDeadline_ReturnsDeadlinePassed(int secondsAfterDeadline)
    {
        var tournament = CreateTournament(maxTeams: 8);

        var reason = tournament.GetRegistrationBlockReason(StartsAt.AddSeconds(secondsAfterDeadline));

        Assert.Equal(RegistrationBlockReason.DeadlinePassed, reason);
    }

    [Fact]
    public void GetRegistrationBlockReason_OneTeamBelowLimit_ReturnsNull()
    {
        var tournament = CreateTournament(maxTeams: 3);
        AddTeams(tournament, 2);

        var reason = tournament.GetRegistrationBlockReason(StartsAt.AddDays(-1));

        Assert.Null(reason);
    }

    [Fact]
    public void GetRegistrationBlockReason_LimitReached_ReturnsLimitReached()
    {
        var tournament = CreateTournament(maxTeams: 3);
        AddTeams(tournament, 3);

        var reason = tournament.GetRegistrationBlockReason(StartsAt.AddDays(-1));

        Assert.Equal(RegistrationBlockReason.LimitReached, reason);
    }

    [Fact]
    public void GetRegistrationBlockReason_StartedTournament_ReturnsTournamentStarted()
    {
        var tournament = CreateTournament(maxTeams: 8);
        tournament.Start();

        var reason = tournament.GetRegistrationBlockReason(StartsAt.AddDays(-1));

        Assert.Equal(RegistrationBlockReason.TournamentStarted, reason);
    }

    [Fact]
    public void GetRegistrationBlockReason_StartedAndPastDeadlineAndFull_ReportsStartedFirst()
    {
        var tournament = CreateTournament(maxTeams: 2);
        AddTeams(tournament, 2);
        tournament.Start();

        var reason = tournament.GetRegistrationBlockReason(StartsAt.AddDays(1));

        Assert.Equal(RegistrationBlockReason.TournamentStarted, reason);
    }

    [Fact]
    public void GetRegistrationBlockReason_PastDeadlineAndFull_ReportsDeadlineBeforeLimit()
    {
        var tournament = CreateTournament(maxTeams: 2);
        AddTeams(tournament, 2);

        var reason = tournament.GetRegistrationBlockReason(StartsAt.AddDays(1));

        Assert.Equal(RegistrationBlockReason.DeadlinePassed, reason);
    }

    private static Tournament CreateTournament(int maxTeams) =>
        new("Liga Szkolna", "Piłka nożna", TournamentFormat.League, StartsAt, maxTeams);

    private static void AddTeams(Tournament tournament, int count)
    {
        for (var i = 1; i <= count; i++)
        {
            tournament.Teams.Add(new Team($"Drużyna {i}", tournament.Id));
        }
    }
}
