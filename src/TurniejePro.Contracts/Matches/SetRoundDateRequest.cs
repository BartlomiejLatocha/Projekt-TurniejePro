namespace TurniejePro.Contracts.Matches;

/// <summary>
/// Nowa data rundy terminarza (<c>PUT /api/v1/tournaments/{id}/rounds/{round}/date</c>).
/// </summary>
/// <param name="Date">Data i godzina rundy (UTC). <c>null</c> usuwa datę („termin do ustalenia”).</param>
public sealed record SetRoundDateRequest(DateTimeOffset? Date);
