namespace TurniejePro.Api.Auth;

public static class Policies
{
    public const string ManageTournaments = "ManageTournaments";
    public const string AcceptApplications = "AcceptApplications";
    public const string RegisterTeam = "RegisterTeam";
    public const string EnterResults = "EnterResults";

    /// <summary>
    /// Polityka zasobowa: tylko organizator danego turnieju albo administrator. Sprawdzana w kontrolerze przez
    /// <c>IAuthorizationService.AuthorizeAsync(User, tournament, Policies.ManageTournament)</c> po wczytaniu turnieju.
    /// </summary>
    public const string ManageTournament = "ManageTournament";
}
