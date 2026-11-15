using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TurniejePro.Api.Auth;
using TurniejePro.Api.Startup;
using TurniejePro.Contracts.Auth;
using TurniejePro.Data;
using TurniejePro.Data.Identity;

namespace TurniejePro.Tests;

/// <summary>
/// Start aplikacji: migracje i role w każdym środowisku, konto testowe organizatora tylko w Development (KAN-279).
/// </summary>
public class DatabaseInitializerTests
{
    private const string SeedEmail = "organizer@example.com";
    private const string SeedPassword = "Organizer123";

    [Fact]
    public async Task Development_WithSeedPassword_CreatesOrganizerWhoCanLogIn()
    {
        await using var factory = new SeedFactory("Development", SeedPassword);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(SeedEmail, SeedPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.Contains(Roles.User, auth.Roles);
    }

    [Fact]
    public async Task Development_WithoutSeedPassword_DoesNotCreateOrganizer()
    {
        await using var factory = new SeedFactory("Development", seedPassword: null);
        using var scope = factory.Services.CreateScope();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.Null(await userManager.FindByEmailAsync(SeedEmail));
    }

    [Fact]
    public async Task Development_OldHardcodedPassword_NoLongerWorks()
    {
        await using var factory = new SeedFactory("Development", SeedPassword);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(SeedEmail, "Password123!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Production_WithSeedPassword_DoesNotCreateOrganizer()
    {
        await using var factory = new SeedFactory("Production", SeedPassword);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(SeedEmail, SeedPassword));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Production_AppliesMigrationsAndSeedsRoles()
    {
        await using var factory = new SeedFactory("Production", seedPassword: null);
        using var scope = factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        Assert.NotEmpty(await dbContext.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await dbContext.Database.GetPendingMigrationsAsync());
        Assert.All(UserRoles.All, role => Assert.Contains(roleManager.Roles, r => r.Name == role));
    }

    [Fact]
    public async Task SeedOrganizer_CalledTwice_CreatesThenReportsExisting()
    {
        await using var factory = new SeedFactory("Development", seedPassword: null);
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var options = new DevSeedOptions { OrganizerEmail = SeedEmail, OrganizerPassword = SeedPassword };

        var first = await DevelopmentUserSeeder.SeedOrganizerAsync(userManager, options, NullLogger.Instance);
        var second = await DevelopmentUserSeeder.SeedOrganizerAsync(userManager, options, NullLogger.Instance);

        Assert.Equal(DevSeedResult.Created, first);
        Assert.Equal(DevSeedResult.AlreadyExists, second);
        var user = await userManager.FindByEmailAsync(SeedEmail);
        Assert.NotNull(user);
        Assert.True(await userManager.IsInRoleAsync(user, Roles.User));
    }

    [Theory]
    [InlineData(null, SeedPassword)]
    [InlineData(SeedEmail, null)]
    [InlineData(" ", SeedPassword)]
    [InlineData(SeedEmail, " ")]
    public async Task SeedOrganizer_MissingEmailOrPassword_IsSkipped(string? email, string? password)
    {
        await using var factory = new SeedFactory("Development", seedPassword: null);
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var options = new DevSeedOptions { OrganizerEmail = email, OrganizerPassword = password };

        var result = await DevelopmentUserSeeder.SeedOrganizerAsync(userManager, options, NullLogger.Instance);

        Assert.Equal(DevSeedResult.Skipped, result);
        Assert.Empty(userManager.Users);
    }

    [Fact]
    public async Task SeedOrganizer_PasswordRejectedByIdentity_ReturnsFailed()
    {
        await using var factory = new SeedFactory("Development", seedPassword: null);
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var options = new DevSeedOptions { OrganizerEmail = SeedEmail, OrganizerPassword = "abc" };

        var result = await DevelopmentUserSeeder.SeedOrganizerAsync(userManager, options, NullLogger.Instance);

        Assert.Equal(DevSeedResult.Failed, result);
        Assert.Null(await userManager.FindByEmailAsync(SeedEmail));
    }

    [Fact]
    public async Task SeedOrganizer_NullArguments_Throw()
    {
        await using var factory = new SeedFactory("Development", seedPassword: null);
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var options = new DevSeedOptions();

        await Assert.ThrowsAsync<ArgumentNullException>(() => DevelopmentUserSeeder.SeedOrganizerAsync(null!, options, NullLogger.Instance));
        await Assert.ThrowsAsync<ArgumentNullException>(() => DevelopmentUserSeeder.SeedOrganizerAsync(userManager, null!, NullLogger.Instance));
        await Assert.ThrowsAsync<ArgumentNullException>(() => DevelopmentUserSeeder.SeedOrganizerAsync(userManager, options, null!));
    }

    [Fact]
    public async Task InitializeDatabase_NullApp_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => DatabaseInitializer.InitializeDatabaseAsync(null!));
    }

    /// <summary>
    /// Fabryka z wybranym środowiskiem i opcjonalnym hasłem konta testowego.
    /// </summary>
    private sealed class SeedFactory(string environment, string? seedPassword) : CustomWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.UseEnvironment(environment);
            builder.UseSetting("DevSeed:OrganizerEmail", SeedEmail);
            builder.UseSetting("DevSeed:OrganizerPassword", seedPassword ?? string.Empty);
        }
    }
}
