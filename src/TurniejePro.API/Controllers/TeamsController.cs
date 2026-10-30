using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurniejePro.Api.Auth;
using TurniejePro.Api.Mapping;
using TurniejePro.Contracts.Teams;
using TurniejePro.Domain;
using TurniejePro.Domain.Repositories;

namespace TurniejePro.Api.Controllers;

[ApiController]
[Route("api/v1/teams")]
public class TeamsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public TeamsController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Zgłasza nową drużynę do turnieju (Wymaga zalogowania / polityki RegisterTeam).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.RegisterTeam)]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Register([FromBody] CreateTeamRequest request, CancellationToken cancellationToken)
    {
        // 1. Weryfikacja istnienia turnieju
        var tournament = await _unitOfWork.Tournaments.GetWithDetailsAsync(request.TournamentId, cancellationToken);
        if (tournament is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Nie znaleziono turnieju",
                Detail = $"Turniej o identyfikatorze '{request.TournamentId}' nie istnieje."
            });
        }

        // 2. Walidacja biznesowa możliwości zapisów do turnieju
        if (!tournament.CanRegisterTeam())
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Rejestracja niedozwolona",
                Detail = "Turniej został już rozpoczęty lub osiągnięto maksymalny limit zarejestrowanych drużyn."
            });
        }

        // 3. Sprawdzenie unikalności nazwy drużyny w ramach danego turnieju
        var nameExists = await _unitOfWork.Teams.ExistsByNameInTournamentAsync(request.TournamentId, request.Name, cancellationToken);
        if (nameExists)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Nazwa drużyny zajęta",
                Detail = $"Drużyna o nazwie '{request.Name}' jest już zgłoszona do tego turnieju."
            });
        }

        // 4. Pobranie ID zalogowanego użytkownika (Kapitana)
        Guid? captainId = null;
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (Guid.TryParse(userIdClaim, out var parsedGuid))
        {
            captainId = parsedGuid;
        }

        try
        {
            // 5. Utworzenie encji drużyny oraz dodanie zawodników
            var team = new Team(request.Name, request.TournamentId, captainId, request.ContactEmail);

            if (request.Players is not null)
            {
                foreach (var playerDto in request.Players)
                {
                    team.AddPlayer(playerDto.FullName, playerDto.JerseyNumber);
                }
            }

            await _unitOfWork.Teams.AddAsync(team, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = team.Id }, team.ToResponse());
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Błąd walidacji zgłoszenia",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Pobiera szczegółowe informacje o zgłoszonej drużynie.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var team = await _unitOfWork.Teams.GetWithPlayersAsync(id, cancellationToken);

        if (team is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Nie znaleziono drużyny",
                Detail = $"Drużyna o identyfikatorze '{id}' nie istnieje."
            });
        }

        return Ok(team.ToResponse());
    }

    /// <summary>
    /// Pobiera listę wszystkich drużyn zarejestrowanych w danym turnieju.
    /// </summary>
    [HttpGet("tournament/{tournamentId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<TeamResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByTournamentId(Guid tournamentId, CancellationToken cancellationToken)
    {
        var teams = await _unitOfWork.Teams.GetByTournamentIdAsync(tournamentId, cancellationToken);
        var response = teams.Select(t => t.ToResponse()).ToList();

        return Ok(response);
    }
}
