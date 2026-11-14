using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurniejePro.Api.Auth;
using TurniejePro.Api.Mapping;
using TurniejePro.Contracts.Matches;
using TurniejePro.Domain.Repositories;
using TurniejePro.Domain.Scheduling;

namespace TurniejePro.Api.Controllers;

[ApiController]
[Route("api/v1/tournaments/{tournamentId:guid}/matches")]
public class MatchesController : ControllerBase
{
    private readonly MatchResultService _matchResultService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthorizationService _authorizationService;

    public MatchesController(
        MatchResultService matchResultService,
        IUnitOfWork unitOfWork,
        IAuthorizationService authorizationService)
    {
        _matchResultService = matchResultService;
        _unitOfWork = unitOfWork;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Wprowadza lub aktualizuje wynik meczu w turnieju. Tylko organizator turnieju lub administrator.
    /// </summary>
    [HttpPut("{matchId:guid}/result")]
    [Authorize(Policy = Policies.EnterResults)]
    [ProducesResponseType(typeof(MatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MatchResponse>> SetResult(
        Guid tournamentId,
        Guid matchId,
        [FromBody] UpdateMatchResultRequest request,
        CancellationToken cancellationToken)
    {
        var tournament = await _unitOfWork.Tournaments.GetByIdAsync(tournamentId, cancellationToken);
        if (tournament is null)
        {
            return Problem(
                detail: $"Nie odnaleziono turnieju o ID '{tournamentId}'.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Resource Not Found");
        }

        var authorization = await _authorizationService.AuthorizeAsync(User, tournament, Policies.ManageTournament);
        if (!authorization.Succeeded)
        {
            return Forbid();
        }

        try
        {
            var updatedMatch = await _matchResultService.SetResultAsync(
                tournamentId,
                matchId,
                request.HomeScore,
                request.AwayScore,
                cancellationToken);

            return Ok(updatedMatch.ToResponse());
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound,
                title: "Resource Not Found");
        }
        catch (MatchResultConflictException ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict,
                title: "Match Result Conflict");
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Score Request");
        }
    }
}
