using TurniejePro.Contracts.Standings;
using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using TurniejePro.Domain.Standings;

namespace TurniejePro.Api.Mapping;

internal static class StandingsMappingExtensions
{
    internal static StandingsResponse ToStandingsResponse(
        this Tournament tournament, IReadOnlyList<Team> teams, IReadOnlyList<Match> matches)
    {
        // Faza grupowa: tabela osobno dla każdej grupy (mecze grupowe). Pozostałe formaty: jedna tabela.
        var table = tournament.Format == TournamentFormat.GroupsAndCup
            ? teams.Where(t => t.GroupNumber is not null)
                .GroupBy(t => t.GroupNumber!.Value)
                .OrderBy(g => g.Key)
                .SelectMany(g => StandingsCalculator
                    .Calculate(g.Select(t => t.Id).ToList(), matches.Where(m => m.IsGroupStage).ToList(), tournament.ScoringRules)
                    .Select(r => (Row: r, Group: (int?)g.Key)))
                .ToList()
            : StandingsCalculator
                .Calculate(teams.Select(t => t.Id).ToList(), matches, tournament.ScoringRules)
                .Select(r => (Row: r, Group: (int?)null))
                .ToList();
        var names = teams.ToDictionary(t => t.Id, t => t.Name);
        var logos = teams.ToDictionary(t => t.Id, t => t.HasLogo);

        return new StandingsResponse(
            tournament.Id,
            table.Select(x => new StandingsRowResponse(
                x.Row.Place,
                x.Row.TeamId,
                names[x.Row.TeamId],
                x.Row.Played,
                x.Row.Won,
                x.Row.Drawn,
                x.Row.Lost,
                x.Row.GoalsFor,
                x.Row.GoalsAgainst,
                x.Row.GoalDifference,
                x.Row.Points,
                logos[x.Row.TeamId],
                x.Group)).ToList());
    }
}
