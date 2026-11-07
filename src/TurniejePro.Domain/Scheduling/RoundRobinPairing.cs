namespace TurniejePro.Domain.Scheduling;

/// <summary>
/// Para drużyn rozgrywająca jeden mecz w terminarzu ligowym.
/// </summary>
/// <param name="HomeTeamId">Gospodarz.</param>
/// <param name="AwayTeamId">Gość.</param>
public sealed record RoundRobinPairing(Guid HomeTeamId, Guid AwayTeamId);
