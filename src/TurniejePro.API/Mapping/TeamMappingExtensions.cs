using TurniejePro.Contracts.Teams;
using TurniejePro.Domain;

namespace TurniejePro.Api.Mapping;

public static class TeamMappingExtensions
{
    public static TeamResponse ToResponse(this Team team)
    {
        var players = team.Players?
            .Select(p => p.ToResponse())
            .ToList() ?? new List<PlayerResponse>();

        return new TeamResponse(
            team.Id,
            team.TournamentId,
            team.Name,
            team.CaptainId,
            team.ContactEmail,
            (Contracts.Teams.TeamRegistrationStatus)team.Status,
            team.RegisteredAt,
            players);
    }

    public static PlayerResponse ToResponse(this Player player)
    {
        return new PlayerResponse(
            player.Id,
            player.FullName,
            player.JerseyNumber);
    }
}
