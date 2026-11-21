using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurniejePro.Api.Mapping;
using TurniejePro.Contracts.Profile;
using TurniejePro.Domain.Repositories;

namespace TurniejePro.Api.Controllers;

/// <summary>
/// Zasoby zalogowanego użytkownika: turnieje, które zorganizował, i drużyny, których jest kapitanem (KAN-304).
/// Każdy widzi tylko swoje, także administrator.
/// </summary>
[ApiController]
[Route("api/v1/me")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public MeController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Turnieje, których zalogowany użytkownik jest organizatorem, także szkice. Najpierw turnieje w trakcie,
    /// potem zapisy, szkice i zakończone; w obrębie etapu według terminu rozpoczęcia.
    /// </summary>
    [HttpGet("tournaments")]
    [ProducesResponseType(typeof(IReadOnlyList<MyTournamentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<MyTournamentResponse>>> GetMyTournaments(CancellationToken cancellationToken)
    {
        if (CurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var tournaments = await _unitOfWork.Tournaments.GetByOrganizerIdAsync(userId, cancellationToken);

        return Ok(tournaments
            .OrderBy(t => ProfileMappingExtensions.DisplayOrder(t.Status))
            .ThenBy(t => t.StartsAt)
            .ThenBy(t => t.Name, StringComparer.CurrentCulture)
            .Select(t => t.ToMyTournamentResponse())
            .ToList());
    }

    /// <summary>
    /// Drużyny, których zalogowany użytkownik jest kapitanem, ze stanem zgłoszenia i danymi turnieju. Kolejność według
    /// etapu turnieju (w trakcie, zapisy, szkic, zakończony), a w obrębie etapu według terminu rozpoczęcia.
    /// </summary>
    [HttpGet("teams")]
    [ProducesResponseType(typeof(IReadOnlyList<MyTeamResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<MyTeamResponse>>> GetMyTeams(CancellationToken cancellationToken)
    {
        if (CurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var teams = await _unitOfWork.Teams.GetByCaptainIdAsync(userId, cancellationToken);

        return Ok(teams
            .Where(t => t.Tournament is not null)
            .OrderBy(t => ProfileMappingExtensions.DisplayOrder(t.Tournament!.Status))
            .ThenBy(t => t.Tournament!.StartsAt)
            .ThenBy(t => t.Name, StringComparer.CurrentCulture)
            .Select(t => t.ToMyTeamResponse())
            .ToList());
    }

    private Guid? CurrentUserId()
    {
        var subject = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(subject, out var userId) ? userId : null;
    }
}
