namespace TurniejePro.Contracts.Matches;

/// <summary>
/// Jedna runda terminarza.
/// </summary>
/// <param name="Round">Numer rundy (od 1).</param>
/// <param name="Matches">Mecze rundy, posortowane według pozycji.</param>
/// <param name="Date">Data i godzina rundy (UTC), wspólna dla jej meczów; null, gdy runda nie ma daty
/// („termin do ustalenia”).</param>
public sealed record ScheduleRoundResponse(
    int Round,
    IReadOnlyList<MatchResponse> Matches,
    DateTimeOffset? Date = null);
