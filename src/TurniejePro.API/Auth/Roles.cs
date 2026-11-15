using TurniejePro.Data.Identity;

namespace TurniejePro.Api.Auth;

/// <summary>
/// Nazwy ról używane w politykach i w tokenach. Wartości pochodzą z seeda ról (KAN-64),
/// żeby polityki i role w bazie zawsze były zgodne.
/// </summary>
public static class Roles
{
    public const string Admin = UserRoles.Admin;
    public const string User = UserRoles.User;
}
