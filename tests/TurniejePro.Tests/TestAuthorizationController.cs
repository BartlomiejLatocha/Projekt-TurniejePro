using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurniejePro.Api.Auth;

namespace TurniejePro.Tests.Controllers;

[ApiController]
[Route("api/test-auth")]
public class TestAuthorizationController : ControllerBase
{
    [HttpGet("organizer-only")]
    [Authorize(Policy = Policies.ManageTournaments)]
    public IActionResult OrganizerOnly() => Ok("Dostęp przyznany dla Organizatora");

    [HttpGet("captain-or-organizer")]
    [Authorize(Policy = Policies.RegisterTeam)]
    public IActionResult CaptainOrOrganizer() => Ok("Dostęp przyznany dla Kapitana lub Organizatora");

    [HttpGet("accept-applications")]
    [Authorize(Policy = Policies.AcceptApplications)]
    public IActionResult AcceptApplications() => Ok("Dostęp przyznany dla Organizatora (zgłoszenia)");

    [HttpGet("enter-results")]
    [Authorize(Policy = Policies.EnterResults)]
    public IActionResult EnterResults() => Ok("Dostęp przyznany dla Kapitana lub Organizatora (wyniki)");

    // Brak atrybutów autoryzacji: o dostępie decyduje polityka domyślna (FallbackPolicy = wymagane logowanie).
    [HttpGet("no-attributes")]
    public IActionResult NoAttributes() => Ok("Dostęp przyznany każdemu zalogowanemu");
}
