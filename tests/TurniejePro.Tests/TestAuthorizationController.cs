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

    // EnterResults jest polityką zasobową (KAN-285): sprawdza ją prawdziwy endpoint wyników po wczytaniu turnieju,
    // więc nie ma tu endpointu testowego. Zobacz TournamentOwnershipIntegrationTests.

    // Brak atrybutów autoryzacji: o dostępie decyduje polityka domyślna (FallbackPolicy = wymagane logowanie).
    [HttpGet("no-attributes")]
    public IActionResult NoAttributes() => Ok("Dostęp przyznany każdemu zalogowanemu");
}
