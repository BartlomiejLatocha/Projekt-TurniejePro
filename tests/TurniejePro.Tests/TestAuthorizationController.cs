using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurniejePro.Api.Auth;

namespace TurniejePro.Tests.Controllers;

[ApiController]
[Route("api/test-auth")]
public class TestAuthorizationController : ControllerBase
{
    [HttpGet("create-tournament")]
    [Authorize(Policy = Policies.CreateTournament)]
    public IActionResult CreateTournament() => Ok("Dostęp przyznany każdemu zalogowanemu (tworzenie turnieju)");

    [HttpGet("register-team")]
    [Authorize(Policy = Policies.RegisterTeam)]
    public IActionResult RegisterTeam() => Ok("Dostęp przyznany każdemu zalogowanemu (zgłoszenie drużyny)");

    // EnterResults jest polityką zasobową (KAN-285): sprawdza ją prawdziwy endpoint wyników po wczytaniu turnieju,
    // więc nie ma tu endpointu testowego. Zobacz TournamentOwnershipIntegrationTests.

    // Brak atrybutów autoryzacji: o dostępie decyduje polityka domyślna (FallbackPolicy = wymagane logowanie).
    [HttpGet("no-attributes")]
    public IActionResult NoAttributes() => Ok("Dostęp przyznany każdemu zalogowanemu");
}
