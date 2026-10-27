namespace TurniejePro.Contracts.Auth;

/// <summary>
/// Nazwy ról zwracane przez API w <see cref="AuthResponse.Roles"/>.
/// Muszą być identyczne z wartościami w TurniejePro.Data.Identity.UserRoles.
/// </summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Organizer = "Organizer";
    public const string Participant = "Participant";
    public const string Spectator = "Spectator";
}
