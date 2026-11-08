using TurniejePro.Contracts.Tournaments;

namespace TurniejePro.Contracts.Matches;

/// <summary>
/// Terminarz turnieju pogrupowany w rundy.
/// </summary>
/// <param name="TournamentId">Identyfikator turnieju.</param>
/// <param name="Format">System rozgrywek (liga albo puchar).</param>
/// <param name="Rounds">Rundy terminarza, posortowane rosnąco.</param>
public sealed record ScheduleResponse(
    Guid TournamentId,
    TournamentFormat Format,
    IReadOnlyList<ScheduleRoundResponse> Rounds);
