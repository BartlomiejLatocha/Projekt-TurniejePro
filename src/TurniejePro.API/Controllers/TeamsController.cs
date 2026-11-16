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
    private readonly IAuthorizationService _authorizationService;

    public TeamsController(IUnitOfWork unitOfWork, IAuthorizationService authorizationService)
    {
        _unitOfWork = unitOfWork;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Zgłasza nową drużynę do turnieju (Wymaga polityki RegisterTeam).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.RegisterTeam)]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Register([FromBody] CreateTeamRequest request, CancellationToken cancellationToken)
    {
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

        if (tournament.GetRegistrationBlockReason(DateTimeOffset.UtcNow) is { } blockReason)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Rejestracja niedozwolona",
                Detail = DescribeRegistrationBlock(blockReason, tournament.MaxTeams)
            });
        }

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

        Guid? captainId = null;
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (Guid.TryParse(userIdClaim, out var parsedGuid))
        {
            captainId = parsedGuid;
        }

        // Błędne dane drużyny lub zawodników (ArgumentException, BusinessRuleException) zamienia na 400 globalny handler
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

    /// <summary>
    /// Akceptuje zgłoszenie drużyny (Wymaga polityki AcceptApplications).
    /// </summary>
    [HttpPatch("{id:guid}/accept")]
    [Authorize]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AcceptTeam(Guid id, CancellationToken cancellationToken)
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

        var tournament = await _unitOfWork.Tournaments.GetWithDetailsAsync(team.TournamentId, cancellationToken);
        if (tournament is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Nie znaleziono turnieju",
                Detail = "Turniej powiązany z tą drużyną nie istnieje."
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
                Title = "Turniej rozpoczęty",
                Detail = "Nie można akceptować zgłoszeń w turnieju, który został już rozpoczęty."
            });
        }

        // Sprawdzenie, czy dla turnieju wygenerowano już terminarz
        var hasSchedule = await _unitOfWork.Matches.HasScheduleAsync(team.TournamentId, cancellationToken);
        if (hasSchedule)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Terminarz już istnieje",
                Detail = "Nie można akceptować zgłoszeń po wygenerowaniu terminarza turnieju."
            });
        }

        // Sprawdzanie limitu MaxTeams dla drużyn zaakceptowanych
        var acceptedTeams = await _unitOfWork.Teams.GetByStatusAsync(
            team.TournamentId,
            Domain.Enums.TeamRegistrationStatus.Accepted,
            cancellationToken);

        if (team.Status != Domain.Enums.TeamRegistrationStatus.Accepted && acceptedTeams.Count >= tournament.MaxTeams)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Osiągnięto limit drużyn",
                Detail = $"Maksymalna liczba zaakceptowanych drużyn ({tournament.MaxTeams}) została już osiągnięta."
            });
        }

        team.Accept();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(team.ToResponse());
    }

    /// <summary>
    /// Odrzuca zgłoszenie drużyny (Wymaga polityki AcceptApplications).
    /// </summary>
    [HttpPatch("{id:guid}/reject")]
    [Authorize]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RejectTeam(Guid id, CancellationToken cancellationToken)
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

        var tournament = await _unitOfWork.Tournaments.GetWithDetailsAsync(team.TournamentId, cancellationToken);
        if (tournament is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Nie znaleziono turnieju",
                Detail = "Turniej powiązany z tą drużyną nie istnieje."
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
                Detail = "Nie można odrzucić zgłoszenia w rozpoczętym turnieju."
            });
        }

        team.Reject();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(team.ToResponse());
    }

    /// <summary>
    /// Pobiera szczegóły drużyny po ID.
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
    /// Pobiera drużyny z danego turnieju.
    /// </summary>
    [HttpGet("tournament/{tournamentId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<TeamResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByTournamentId(Guid tournamentId, CancellationToken cancellationToken)
    {
        var teams = await _unitOfWork.Teams.GetByTournamentIdAsync(tournamentId, cancellationToken);
        return Ok(teams.Select(t => t.ToResponse()).ToList());
    }

    private static string DescribeRegistrationBlock(Domain.Enums.RegistrationBlockReason reason, int maxTeams) => reason switch
    {
        Domain.Enums.RegistrationBlockReason.TournamentStarted => "Turniej został już rozpoczęty.",
        Domain.Enums.RegistrationBlockReason.DeadlinePassed => "Upłynął termin zgłoszeń (termin rozpoczęcia turnieju).",
        Domain.Enums.RegistrationBlockReason.LimitReached => $"Osiągnięto maksymalny limit zarejestrowanych drużyn ({maxTeams}).",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Nieznany powód blokady zgłoszeń.")
    };

    /// <summary>
    /// Wysyła logo drużyny (PNG lub JPG, najwyżej 1 MB). Może je zmienić kapitan drużyny albo organizator turnieju.
    /// </summary>
    [HttpPut("{id:guid}/logo")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(Team.MaxLogoBytes + 4096)]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadLogo(Guid id, IFormFile? file, CancellationToken cancellationToken)
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

        var tournament = await _unitOfWork.Tournaments.GetByIdAsync(team.TournamentId, cancellationToken);
        if (tournament is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Nie znaleziono turnieju",
                Detail = "Turniej powiązany z tą drużyną nie istnieje."
            });
        }

        var isCaptain = Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value, out var userId)
            && team.CaptainId == userId;
        if (!isCaptain)
        {
            var authorization = await _authorizationService.AuthorizeAsync(User, tournament, Policies.ManageTournament);
            if (!authorization.Succeeded)
            {
                return Forbid();
            }
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Brak pliku logo",
                Detail = "Dołącz plik logo (PNG lub JPG)."
            });
        }

        if (file.Length > Team.MaxLogoBytes)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Logo jest za duże",
                Detail = "Logo może mieć najwyżej 1 MB."
            });
        }

        var data = await ReadAllAsync(file, cancellationToken);
        var contentType = DetectImageType(data);
        if (contentType is null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Nieobsługiwany format",
                Detail = "Logo musi być plikiem PNG lub JPG."
            });
        }

        team.SetLogo(contentType, data);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(team.ToResponse());
    }

    /// <summary>
    /// Pobiera logo drużyny jako obraz (PNG lub JPG).
    /// </summary>
    [HttpGet("{id:guid}/logo")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLogo(Guid id, CancellationToken cancellationToken)
    {
        var team = await _unitOfWork.Teams.GetByIdAsync(id, cancellationToken);
        if (team?.Logo is null || team.LogoContentType is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Brak logo",
                Detail = "Drużyna nie ma przesłanego logo."
            });
        }

        Response.Headers.CacheControl = "public,max-age=300";
        return File(team.Logo, team.LogoContentType);
    }

    private static async Task<byte[]> ReadAllAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        return memory.ToArray();
    }

    // Typ rozpoznajemy po sygnaturze pliku, a nie po nagłówku przysłanym przez klienta.
    private static string? DetectImageType(byte[] data)
    {
        if (data.Length >= 4 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
        {
            return "image/png";
        }

        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return "image/jpeg";
        }

        return null;
    }
}
