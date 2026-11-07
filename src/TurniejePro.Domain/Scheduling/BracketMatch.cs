namespace TurniejePro.Domain.Scheduling;

/// <summary>
/// Mecz drabinki pucharowej.
/// </summary>
/// <param name="Position">Numer meczu w rundzie, liczony od góry drabinki od 1.</param>
/// <param name="HomeTeamId">Gospodarz (wyżej rozstawiony) albo null, gdy jeszcze nie wiadomo, kto zagra.</param>
/// <param name="AwayTeamId">Gość albo null, gdy jeszcze nie wiadomo, kto zagra, lub gdy to wolny los.</param>
/// <param name="IsBye">Wolny los: drużyna gospodarza przechodzi dalej bez gry.</param>
/// <param name="WinnerId">Zwycięzca meczu albo null, gdy mecz nie został rozstrzygnięty. Przy wolnym losie to gospodarz.</param>
public sealed record BracketMatch(int Position, Guid? HomeTeamId, Guid? AwayTeamId, bool IsBye, Guid? WinnerId);
