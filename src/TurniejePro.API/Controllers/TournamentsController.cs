using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurniejePro.Api.Auth;
using TurniejePro.Contracts.Tournaments;

namespace TurniejePro.Api.Controllers;

[ApiController]
[Route("api/v1/tournaments")]
public class TournamentsController : ControllerBase
{
    // 1. PUBLICZNE (Dostępne dla niezalogowanych i kibiców)
    [HttpGet]
    [AllowAnonymous]
    public IActionResult GetAll()
    {
        // Zwrot listy turniejów
        return Ok();
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public IActionResult GetById(Guid id)
    {
        // Zwrot szczegółów turnieju
        return Ok();
    }

    // 2. CHRONIONE (Wymagają odpowiedniej roli / polityki)
    [HttpPost]
    [Authorize(Policy = Policies.ManageTournaments)]
    public IActionResult Create([FromBody] CreateTournamentRequest request)
    {
        // Tworzenie turnieju (tylko Organizator)
        return Created("", null);
    }
}
