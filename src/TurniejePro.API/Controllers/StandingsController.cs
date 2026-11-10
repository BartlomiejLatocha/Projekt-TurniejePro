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
                title: "Tournament Not Found");
        }

        var teams = await _unitOfWork.Teams.GetByStatusAsync(
            tournamentId,
            Domain.Enums.TeamRegistrationStatus.Accepted,
            cancellationToken);

        var matches = await _unitOfWork.Matches.GetByTournamentIdAsync(tournamentId, cancellationToken);

        var rows = StandingsCalculator.Calculate(teams.Select(t => t.Id).ToList(), matches);
        var names = teams.ToDictionary(t => t.Id, t => t.Name);

        var response = new StandingsResponse(
            tournamentId,
            rows.Select(r => new StandingsRowResponse(
                r.Place,
                r.TeamId,
                names[r.TeamId],
                r.Played,
                r.Won,
                r.Drawn,
                r.Lost,
                r.GoalsFor,
                r.GoalsAgainst,
                r.GoalDifference,
                r.Points)).ToList());

        return Ok(response);
    }
}
