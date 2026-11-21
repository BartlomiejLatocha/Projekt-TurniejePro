using TurniejePro.Contracts.Profile;
using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using ContractFormat = TurniejePro.Contracts.Tournaments.TournamentFormat;
using ContractStatus = TurniejePro.Contracts.Tournaments.TournamentStatus;
using ContractTeamStatus = TurniejePro.Contracts.Teams.TeamRegistrationStatus;

namespace TurniejePro.Api.Mapping;

/// <summary>
/// Mapowanie widoków „Moje turnieje” i „Moje drużyny” wraz z kolejnością: najpierw to, co wymaga uwagi.
/// </summary>
public static class ProfileMappingExtensions
{
    public static MyTournamentResponse ToMyTournamentResponse(this Tournament tournament)
    {
        ArgumentNullException.ThrowIfNull(tournament);

        return new MyTournamentResponse(
            tournament.Id,
            tournament.Name,
            tournament.Discipline,
            (ContractFormat)tournament.Format,
            (ContractStatus)tournament.Status,
            tournament.StartsAt,
            tournament.MaxTeams,
            tournament.Teams.Count(team => team.Status == TeamRegistrationStatus.Accepted),
            tournament.Teams.Count(team => team.Status == TeamRegistrationStatus.Pending));
    }

    /// <summary>
    /// Wymaga wczytanego <see cref="Team.Tournament"/>.
    /// </summary>
    public static MyTeamResponse ToMyTeamResponse(this Team team)
    {
        ArgumentNullException.ThrowIfNull(team);

        var tournament = team.Tournament
            ?? throw new InvalidOperationException("Do widoku „Moje drużyny” potrzebny jest wczytany turniej drużyny.");

        return new MyTeamResponse(
            team.Id,
            team.Name,
            (ContractTeamStatus)team.Status,
            team.RegisteredAt,
            team.Players.Count,
            team.HasLogo,
            tournament.Id,
            tournament.Name,
            tournament.Discipline,
            (ContractFormat)tournament.Format,
            (ContractStatus)tournament.Status,
            tournament.StartsAt);
    }

    /// <summary>
    /// Kolejność na liście: turnieje w trakcie, potem zapisy, szkice i na końcu zakończone; w obrębie etapu według terminu.
    /// </summary>
    public static int DisplayOrder(TournamentStatus status) => status switch
    {
        TournamentStatus.InProgress => 0,
        TournamentStatus.RegistrationOpen => 1,
        TournamentStatus.Draft => 2,
        _ => 3
    };
}
