namespace TurniejePro.Api.Auth;

public static class Policies
{
    public const string ManageTournaments = "ManageTournaments";
    public const string AcceptApplications = "AcceptApplications";
    public const string RegisterTeam = "RegisterTeam";

    /// <summary>
    /// Polityka zasobowa: tylko organizator danego turnieju albo administrator. Sprawdzana w kontrolerze przez
    /// <c>IAuthorizationService.AuthorizeAsync(User, tournament, Policies.ManageTournament)</c> po wczytaniu turnieju.
    /// </summary>
    public const string ManageTournament = "ManageTournament";

    /// <summary>
    /// Polityka zasobowa dla wyników meczów: tylko organizator turnieju albo administrator (kapitan nie).
    /// Dziś ma to samo wymaganie co <see cref="ManageTournament"/>, ale jest osobna, żeby zmiana zasad wpisywania
    /// wyników (np. zgłaszanie wyniku przez kapitana) dotyczyła jednego miejsca. Sprawdzana w kontrolerze przez
    /// <c>IAuthorizationService.AuthorizeAsync(User, tournament, Policies.EnterResults)</c>, nie przez atrybut.
    /// </summary>
    public const string EnterResults = "EnterResults";
}
