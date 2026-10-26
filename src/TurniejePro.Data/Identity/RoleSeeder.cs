using Microsoft.AspNetCore.Identity;

namespace TurniejePro.Data.Identity;

/// <summary>
/// Klasa odpowiadająca za początkowe wypełnienie bazy danymi ról (KAN-64).
/// </summary>
public static class RoleSeeder
{
    public static async Task SeedRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        ArgumentNullException.ThrowIfNull(roleManager);

        foreach (var roleName in UserRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            }
        }
    }
}
