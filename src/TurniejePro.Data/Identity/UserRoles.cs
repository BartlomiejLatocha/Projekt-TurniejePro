namespace TurniejePro.Data.Identity;

/// <summary>
/// Stałe określające role w systemie TurniejePro (KAN-64).
/// </summary>
public static class UserRoles
{
    /// <summary>Administrator całego systemu.</summary>
    public const string Admin = "Admin";

    /// <summary>Organizator turniejów.</summary>
    public const string Organizer = "Organizer";

    /// <summary>Kapitan drużyny / uczestnik zgłaszający drużynę.</summary>
    public const string Participant = "Participant";

    /// <summary>Kibic z uprawnieniami do podglądu i zbierania tokenów.</summary>
    public const string Spectator = "Spectator";

    public static readonly IReadOnlyList<string> All =
    [
        Admin,
        Organizer,
        Participant,
        Spectator
    ];
}
