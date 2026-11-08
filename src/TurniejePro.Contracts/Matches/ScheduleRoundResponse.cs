namespace TurniejePro.Contracts.Matches;

/// <summary>
/// Jedna runda terminarza.
/// </summary>
/// <param name="Round">Numer rundy (od 1).</param>
/// <param name="Matches">Mecze rundy, posortowane według pozycji.</param>
public sealed record ScheduleRoundResponse(
    int Round,
    IReadOnlyList<MatchResponse> Matches);
