namespace TurniejePro.Data.Identity;

/// <summary>
/// Stałe określające role w systemie TurniejePro (KAN-64).
/// </summary>
public static class UserRoles
{
    /// <summary>Administrator całego systemu.</summary>
    public const string Admin = "Admin";

    public const string User = "User";

    public static readonly IReadOnlyList<string> All =
    [
        Admin,
        User
    ];
}
