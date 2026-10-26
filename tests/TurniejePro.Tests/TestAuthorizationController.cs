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
}
