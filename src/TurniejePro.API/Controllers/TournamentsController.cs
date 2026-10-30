using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurniejePro.Api.Auth;
using TurniejePro.Api.Mapping;
using TurniejePro.Contracts.Common;
using TurniejePro.Contracts.Tournaments;
using TurniejePro.Domain;
using TurniejePro.Domain.Repositories;

namespace TurniejePro.Api.Controllers;

[ApiController]
[Route("api/v1/tournaments")]
public class TournamentsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public TournamentsController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Pobiera stronicowaną listę turniejów z opcjonalnym wyszukiwaniem.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResponse<TournamentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var (items, totalCount) = await _unitOfWork.Tournaments.GetPagedAsync(page, pageSize, search, cancellationToken);

        var responseItems = items.Select(t => t.ToResponse()).ToList();
        var response = new PagedResponse<TournamentResponse>(responseItems, page, pageSize, totalCount);

        return Ok(response);
    }

    /// <summary>
    /// Pobiera szczegółowe informacje o wybranym turnieju.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TournamentDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var tournament = await _unitOfWork.Tournaments.GetWithDetailsAsync(id, cancellationToken);

        if (tournament is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Nie znaleziono turnieju",
                Detail = $"Turniej o identyfikatorze '{id}' nie istnieje."
            });
        }

        return Ok(tournament.ToDetailsResponse());
    }

    /// <summary>
    /// Tworzy nowy turniej (wymaga roli Organizatora).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.ManageTournaments)]
    [ProducesResponseType(typeof(TournamentDetailsResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateTournamentRequest request, CancellationToken cancellationToken)
    {
        Guid? organizerId = null;
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (Guid.TryParse(userIdClaim, out var parsedGuid))
        {
            organizerId = parsedGuid;
        }

        try
        {
            var tournament = new Tournament(
                request.Name,
                request.Discipline,
                (Domain.Enums.TournamentFormat)request.Format,
                request.StartsAt,
                request.MaxTeams,
                organizerId);

            await _unitOfWork.Tournaments.AddAsync(tournament, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var response = tournament.ToDetailsResponse();

            return CreatedAtAction(nameof(GetById), new { id = tournament.Id }, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Błąd walidacji biznesowej",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Aktualizuje istniejący turniej.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.ManageTournaments)]
    [ProducesResponseType(typeof(TournamentDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTournamentRequest request, CancellationToken cancellationToken)
    {
        var tournament = await _unitOfWork.Tournaments.GetWithDetailsAsync(id, cancellationToken);

        if (tournament is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Nie znaleziono turnieju",
                Detail = $"Turniej o identyfikatorze '{id}' nie istnieje."
            });
        }

        try
        {
            tournament.UpdateDetails(
                request.Name,
                request.Discipline,
                (Domain.Enums.TournamentFormat)request.Format,
                request.StartsAt,
                request.MaxTeams);

            _unitOfWork.Tournaments.Update(tournament);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Ok(tournament.ToDetailsResponse());
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Błąd modyfikacji turnieju",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Usuwa turniej z systemu.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.ManageTournaments)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var tournament = await _unitOfWork.Tournaments.GetByIdAsync(id, cancellationToken);

        if (tournament is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Nie znaleziono turnieju",
                Detail = $"Turniej o identyfikatorze '{id}' nie istnieje."
            });
        }

        if (tournament.IsStarted)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Operacja niedozwolona",
                Detail = "Nie można usunąć turnieju, który został już rozpoczęty."
            });
        }

        _unitOfWork.Tournaments.Delete(tournament);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
