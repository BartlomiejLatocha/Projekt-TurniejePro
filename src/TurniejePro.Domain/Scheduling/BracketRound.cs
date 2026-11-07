namespace TurniejePro.Domain.Scheduling;

/// <summary>
/// Jedna runda drabinki pucharowej.
/// </summary>
/// <param name="Number">Numer rundy (od 1; ostatnia runda to finał).</param>
/// <param name="Matches">Mecze rundy w kolejności od góry drabinki.</param>
public sealed record BracketRound(int Number, IReadOnlyList<BracketMatch> Matches);
