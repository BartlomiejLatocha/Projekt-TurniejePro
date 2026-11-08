using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurniejePro.Api.Auth;
using TurniejePro.Api.Mapping;
using TurniejePro.Contracts.Matches;
using TurniejePro.Contracts.Tournaments;
using TurniejePro.Domain.Repositories;
using TurniejePro.Domain.Scheduling;
    
namespace TurniejePro.Api.Controllers;

[ApiController]
[Route("api/v1/tournaments/{tournamentId:guid}/schedule")]
public class ScheduleController : ControllerBase
{
    private readonly ScheduleService _scheduleService;
    private readonly IUnitOfWork _unitOfWork;

    public ScheduleController(ScheduleService scheduleService, IUnitOfWork unitOfWork)
    {
        _scheduleService = scheduleService;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Generuje i zapisuje terminarz turnieju (ligowy lub pucharowy).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.AcceptApplications)]
    [ProducesResponseType(typeof(ScheduleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ScheduleResponse>> GenerateSchedule(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var tournament = await _unitOfWork.Tournaments.GetByIdAsync(tournamentId, cancellationToken);
            if (tournament is null)
            {
                return Problem(
                    detail: $"Nie odnaleziono turnieju o ID '{tournamentId}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Tournament Not Found");
            }

            var matches = await _scheduleService.GenerateScheduleAsync(tournamentId, cancellationToken);

            var contractFormat = (TournamentFormat)tournament.Format;
            var response = matches.ToScheduleResponse(tournamentId, contractFormat);

            return CreatedAtAction(nameof(GetSchedule), new { tournamentId }, response);
        }
        catch (ScheduleAlreadyExistsException ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict,
                title: "Schedule Conflict");
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound,
                title: "Tournament Not Found");
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Schedule Request");
        }
    }

    /// <summary>
    /// Pobiera terminarz meczów dla danego turnieju pogrupowany w rundy.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ScheduleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScheduleResponse>> GetSchedule(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var tournament = await _unitOfWork.Tournaments.GetByIdAsync(tournamentId, cancellationToken);
        if (tournament is null)
        {
            return Problem(
                detail: $"Nie odnaleziono turnieju o ID '{tournamentId}'.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Tournament Not Found");
        }

        var matches = await _unitOfWork.Matches.GetByTournamentIdAsync(tournamentId, cancellationToken);
        if (matches.Count == 0)
        {
            return Problem(
                detail: $"Turniej o ID '{tournamentId}' nie posiada wygenerowanego terminarza.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Schedule Not Found");
        }

        var contractFormat = (TournamentFormat)tournament.Format;
        return Ok(matches.ToScheduleResponse(tournamentId, contractFormat));
    }

    /// <summary>
    /// Usuwa terminarz powiązany z danym turniejem.
    /// </summary>
    [HttpDelete]
    [Authorize(Policy = Policies.AcceptApplications)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSchedule(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var tournament = await _unitOfWork.Tournaments.GetByIdAsync(tournamentId, cancellationToken);
        if (tournament is null)
        {
            return Problem(
                detail: $"Nie odnaleziono turnieju o ID '{tournamentId}'.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Tournament Not Found");
        }

        var hasSchedule = await _unitOfWork.Matches.HasScheduleAsync(tournamentId, cancellationToken);
        if (!hasSchedule)
        {
            return Problem(
                detail: $"Turniej o ID '{tournamentId}' nie posiada wygenerowanego terminarza do usunięcia.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Schedule Not Found");
        }

        await _unitOfWork.Matches.DeleteByTournamentIdAsync(tournamentId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
