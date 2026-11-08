namespace TurniejePro.Contracts.Matches;

/// <summary>
/// Drużyna uczestnicząca w meczu.
/// </summary>
/// <param name="Id">Identyfikator drużyny.</param>
/// <param name="Name">Nazwa drużyny.</param>
public sealed record MatchTeamResponse(Guid Id, string Name);
