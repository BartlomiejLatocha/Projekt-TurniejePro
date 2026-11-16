using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurniejePro.Contracts.Standings;
using TurniejePro.Domain.Repositories;
using TurniejePro.Domain.Standings;

namespace TurniejePro.Api.Controllers;

[ApiController]
[Route("api/v1/tournaments/{tournamentId:guid}/standings")]
public class StandingsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public StandingsController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Pobiera tabelę ligową turnieju. Liczona na bieżąco z meczów zaakceptowanych drużyn.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(StandingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStandings(Guid tournamentId, CancellationToken cancellationToken)
    {
        var tournament = await _unitOfWork.Tournaments.GetByIdAsync(tournamentId, cancellationToken);
        if (tournament is null)
        {
            return Problem(
                detail: $"Nie odnaleziono turnieju o ID '{tournamentId}'.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Nie znaleziono turnieju");
        }

        var teams = await _unitOfWork.Teams.GetByStatusAsync(
            tournamentId,
            Domain.Enums.TeamRegistrationStatus.Accepted,
            cancellationToken);

        var matches = await _unitOfWork.Matches.GetByTournamentIdAsync(tournamentId, cancellationToken);

        // Faza grupowa: tabela osobno dla każdej grupy (mecze grupowe). Pozostałe formaty: jedna tabela.
        var table = tournament.Format == TurniejePro.Domain.Enums.TournamentFormat.GroupsAndCup
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

        var response = new StandingsResponse(
            tournamentId,
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

        return Ok(response);
    }
}
