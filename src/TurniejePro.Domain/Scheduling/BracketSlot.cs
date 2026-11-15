namespace TurniejePro.Domain.Scheduling;

/// <summary>
/// Miejsce w następnej rundzie drabinki, do którego trafia zwycięzca meczu.
/// </summary>
/// <param name="RoundNumber">Numer następnej rundy.</param>
/// <param name="Position">Pozycja meczu w następnej rundzie.</param>
/// <param name="IsHome">Czy zwycięzca zostaje gospodarzem (true) czy gościem (false) tego meczu.</param>
public sealed record BracketSlot(int RoundNumber, int Position, bool IsHome);
