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
    private readonly IAuthorizationService _authorizationService;

    public TournamentsController(IUnitOfWork unitOfWork, IAuthorizationService authorizationService)
    {
        _unitOfWork = unitOfWork;
        _authorizationService = authorizationService;
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

        // Turnieje przechowują kod dyscypliny („volleyball”), a użytkownik wpisuje nazwę z UI („siatkówka”)
        var disciplineCodes = DisciplineCatalog.CodesMatching(search);
        var (items, totalCount) = await _unitOfWork.Tournaments.GetPagedAsync(page, pageSize, search, disciplineCodes, cancellationToken);

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
    [Authorize(Policy = Policies.CreateTournament)]
    [ProducesResponseType(typeof(TournamentDetailsResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateTournamentRequest request, CancellationToken cancellationToken)
    {
        Guid? organizerId = null;
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (Guid.TryParse(userIdClaim, out var parsedGuid))
        {
            organizerId = parsedGuid;
        }

        if (DisciplineCatalog.Find(request.Discipline) is null)
        {
            return Problem(
                detail: $"Nieznana dyscyplina '{request.Discipline}'. Wybierz dyscyplinę z listy.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Discipline");
        }

        // Błędne dane turnieju (ArgumentException z encji, także z punktacji) zamienia na 400 globalny handler wyjątków
        var scoringRules = request.ScoringRules is { } rules
            ? new ScoringRules(rules.PointsForWin, rules.PointsForDraw, rules.PointsForLoss, rules.AllowDraws)
            : null;

        var tournament = new Tournament(
            request.Name,
            request.Discipline,
            (Domain.Enums.TournamentFormat)request.Format,
            request.StartsAt,
            request.MaxTeams,
            organizerId,
            scoringRules);

        await _unitOfWork.Tournaments.AddAsync(tournament, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var response = tournament.ToDetailsResponse();

        return CreatedAtAction(nameof(GetById), new { id = tournament.Id }, response);
    }

    /// <summary>
    /// Aktualizuje istniejący turniej. Szkic: wszystkie pola; Zapisy: nazwa, termin, limit; W trakcie: nazwa;
    /// Zakończony: nic. Administrator pomija te ograniczenia (formatu przy terminarzu nie zmieni nikt).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize]
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

        var authorization = await _authorizationService.AuthorizeAsync(User, tournament, Policies.ManageTournament);
        if (!authorization.Succeeded)
        {
            return Forbid();
        }

        // Pola zablokowane w danym statusie i limit mniejszy niż liczba drużyn (BusinessRuleException) oraz błędne dane
        // (ArgumentException) zamienia na 400 globalny handler wyjątków. Admin pomija ograniczenia statusu.
        tournament.UpdateDetails(
            request.Name,
            request.Discipline,
            (Domain.Enums.TournamentFormat)request.Format,
            request.StartsAt,
            request.MaxTeams,
            asAdmin: User.HasClaim("role", Roles.Admin));

        _unitOfWork.Tournaments.Update(tournament);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(tournament.ToDetailsResponse());
    }

    /// <summary>
    /// Szkic → Zapisy: otwiera zgłoszenia drużyn. Wymaga tokenu organizatora.
    /// </summary>
    [HttpPatch("{id:guid}/open-registration")]
    [Authorize]
    [ProducesResponseType(typeof(TournamentDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> OpenRegistration(Guid id, CancellationToken cancellationToken)
    {
        return await ChangeStatusAsync(id, t => t.OpenRegistration(), cancellationToken);
    }

    /// <summary>
    /// Szkic lub Zapisy → W trakcie: rozpoczyna turniej i blokuje zmiany formatu oraz punktacji. Wymaga tokenu organizatora
    /// i wygenerowanego terminarza. Zwykle niepotrzebne: wygenerowanie terminarza samo rozpoczyna turniej.
    /// </summary>
    [HttpPatch("{id:guid}/start")]
    [Authorize]
    [ProducesResponseType(typeof(TournamentDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Start(Guid id, CancellationToken cancellationToken)
    {
        // GetWithDetailsAsync w ChangeStatusAsync wczytuje mecze, więc domena widzi, czy terminarz istnieje
        return await ChangeStatusAsync(id, t => t.StartWithExistingSchedule(), cancellationToken);
    }

    private async Task<IActionResult> ChangeStatusAsync(Guid id, Action<Tournament> change, CancellationToken cancellationToken)
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

        var authorization = await _authorizationService.AuthorizeAsync(User, tournament, Policies.ManageTournament);
        if (!authorization.Succeeded)
        {
            return Forbid();
        }

        // Niedozwolone przejście stanu (BusinessRuleException) zamienia na 400 globalny handler wyjątków
        change(tournament);

        _unitOfWork.Tournaments.Update(tournament);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(tournament.ToDetailsResponse());
    }

    /// <summary>
    /// Usuwa turniej z systemu.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
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

        var authorization = await _authorizationService.AuthorizeAsync(User, tournament, Policies.ManageTournament);
        if (!authorization.Succeeded)
        {
            return Forbid();
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
