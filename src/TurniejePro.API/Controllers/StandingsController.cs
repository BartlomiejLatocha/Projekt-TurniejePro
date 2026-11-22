using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurniejePro.Api.Mapping;
using TurniejePro.Contracts.Standings;
using TurniejePro.Domain.Repositories;

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

        return Ok(tournament.ToStandingsResponse(teams, matches));
    }
}
