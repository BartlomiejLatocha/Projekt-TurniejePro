namespace TurniejePro.Domain.Scheduling;

/// <summary>
/// Jedna kolejka terminarza ligowego.
/// </summary>
/// <param name="Number">Numer kolejki (od 1).</param>
/// <param name="Pairings">Mecze rozgrywane w tej kolejce.</param>
/// <param name="RestingTeamId">Drużyna pauzująca w tej kolejce. Występuje tylko przy nieparzystej liczbie drużyn.</param>
public sealed record RoundRobinRound(int Number, IReadOnlyList<RoundRobinPairing> Pairings, Guid? RestingTeamId);
