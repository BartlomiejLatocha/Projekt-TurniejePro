using Microsoft.AspNetCore.Identity;

namespace TurniejePro.Data.Identity;

/// <summary>
/// Model użytkownika w ASP.NET Core Identity.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
