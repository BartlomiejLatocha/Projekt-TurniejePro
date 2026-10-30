using TurniejePro.Contracts.Tournaments;
using TurniejePro.Domain;

namespace TurniejePro.Api.Mapping;

public static class TournamentMappingExtensions
{
    public static TournamentResponse ToResponse(this Tournament tournament)
    {
        return new TournamentResponse(
            tournament.Id,
            tournament.Name,
            tournament.Discipline,
            (Contracts.Tournaments.TournamentFormat)tournament.Format,
            tournament.StartsAt,
            tournament.MaxTeams);
    }

    public static TournamentDetailsResponse ToDetailsResponse(this Tournament tournament)
    {
        return new TournamentDetailsResponse(
            tournament.Id,
            tournament.Name,
            tournament.Discipline,
            (Contracts.Tournaments.TournamentFormat)tournament.Format,
            tournament.StartsAt,
            tournament.MaxTeams,
            tournament.IsStarted,
            tournament.OrganizerId,
            tournament.Teams?.Count ?? 0);
    }
}
