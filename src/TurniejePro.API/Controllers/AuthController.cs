using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TurniejePro.Api.Auth;
using TurniejePro.Contracts.Auth;
using TurniejePro.Data.Identity;

namespace TurniejePro.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly ITokenService _tokenService;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        ITokenService tokenService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(AuthResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ValidationProblemDetails))]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Konflikt rejestracji",
                Detail = "Użytkownik o podanym adresie e-mail już istnieje."
            });
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }
            return ValidationProblem(ModelState);
        }

        // Nadanie domyślnej roli i sprawdzenie wyniku (ochrona przed cichym błędem)
        const string defaultRole = Roles.Fan;
        if (!await _roleManager.RoleExistsAsync(defaultRole))
        {
            await _roleManager.CreateAsync(new IdentityRole<Guid>(defaultRole));
        }

        await _userManager.AddToRoleAsync(user, Roles.Organizer);

        var roles = await _userManager.GetRolesAsync(user);
        var authResponse = _tokenService.CreateToken(user.Id, user.Email!, roles);

        return CreatedAtAction(nameof(Me), authResponse);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuthResponse))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Unauthorized();
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return Unauthorized();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var authResponse = _tokenService.CreateToken(user.Id, user.Email!, roles);

        return Ok(authResponse);
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Me()
    {
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = User.FindFirst("email")?.Value ?? User.FindFirst(ClaimTypes.Email)?.Value;
        var roles = User.FindAll("role").Select(r => r.Value).ToList();

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        return Ok(new
        {
            Id = userId,
            Email = email,
            Roles = roles
        });
    }
}
