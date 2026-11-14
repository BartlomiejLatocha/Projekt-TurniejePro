namespace TurniejePro.Api.Startup;

/// <summary>
/// Konto testowe organizatora tworzone przy starcie, wyłącznie w środowisku Development.
/// E-mail jest w appsettings.Development.json, a hasło tylko w User Secrets. Bez hasła konto nie powstaje.
/// </summary>
public sealed class DevSeedOptions
{
    public const string SectionName = "DevSeed";

    public string? OrganizerEmail { get; init; }

    public string? OrganizerPassword { get; init; }
}
