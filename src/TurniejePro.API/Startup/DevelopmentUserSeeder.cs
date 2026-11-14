using Microsoft.AspNetCore.Identity;
using TurniejePro.Api.Auth;
using TurniejePro.Data.Identity;

namespace TurniejePro.Api.Startup;

/// <summary>
/// Wynik próby utworzenia konta testowego organizatora.
/// </summary>
public enum DevSeedResult
{
    /// <summary>Brak e-maila lub hasła w konfiguracji, konto nie zostało utworzone.</summary>
    Skipped,

    /// <summary>Konto o tym adresie już istnieje, nic nie zmieniono.</summary>
    AlreadyExists,

    /// <summary>Konto zostało utworzone i dostało rolę organizatora.</summary>
    Created,

    /// <summary>Identity odrzuciło konto (np. hasło nie spełnia wymagań) albo nadanie roli się nie udało.</summary>
    Failed
}

/// <summary>
/// Tworzy konto testowego organizatora na potrzeby pracy lokalnej. Wywoływany tylko w środowisku Development.
/// </summary>
public static partial class DevelopmentUserSeeder
{
    public static async Task<DevSeedResult> SeedOrganizerAsync(
        UserManager<ApplicationUser> userManager,
        DevSeedOptions options,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        if (string.IsNullOrWhiteSpace(options.OrganizerEmail) || string.IsNullOrWhiteSpace(options.OrganizerPassword))
        {
            LogSkipped(logger, DevSeedOptions.SectionName);
            return DevSeedResult.Skipped;
        }

        var email = options.OrganizerEmail.Trim();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return DevSeedResult.AlreadyExists;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var created = await userManager.CreateAsync(user, options.OrganizerPassword);
        if (!created.Succeeded)
        {
            LogFailed(logger, email, Describe(created));
            return DevSeedResult.Failed;
        }

        var roleAdded = await userManager.AddToRoleAsync(user, Roles.Organizer);
        if (!roleAdded.Succeeded)
        {
            LogFailed(logger, email, Describe(roleAdded));
            return DevSeedResult.Failed;
        }

        LogCreated(logger, email);
        return DevSeedResult.Created;
    }

    private static string Describe(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(error => error.Description));

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Konto testowe organizatora nie zostało utworzone: brak {Section}:OrganizerEmail lub {Section}:OrganizerPassword w konfiguracji (hasło ustaw w User Secrets).")]
    private static partial void LogSkipped(ILogger logger, string section);

    [LoggerMessage(Level = LogLevel.Information, Message = "Utworzono konto testowe organizatora {Email}.")]
    private static partial void LogCreated(ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Nie udało się utworzyć konta testowego organizatora {Email}: {Errors}")]
    private static partial void LogFailed(ILogger logger, string email, string errors);
}
