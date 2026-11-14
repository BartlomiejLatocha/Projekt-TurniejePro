using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TurniejePro.Data;
using TurniejePro.Data.Identity;

namespace TurniejePro.Api.Startup;

/// <summary>
/// Przygotowuje bazę danych przy starcie aplikacji.
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// W każdym środowisku stosuje migracje i tworzy role. Konto testowe organizatora powstaje tylko w Development.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var dbContext = services.GetRequiredService<TurniejeProDbContext>();
        await dbContext.Database.MigrateAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        await RoleSeeder.SeedRolesAsync(roleManager);

        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        var options = app.Configuration.GetSection(DevSeedOptions.SectionName).Get<DevSeedOptions>() ?? new DevSeedOptions();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        await DevelopmentUserSeeder.SeedOrganizerAsync(userManager, options, app.Logger);
    }
}
