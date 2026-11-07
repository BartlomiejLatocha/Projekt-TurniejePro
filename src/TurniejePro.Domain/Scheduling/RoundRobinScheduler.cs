namespace TurniejePro.Domain.Scheduling;

/// <summary>
/// Generuje terminarz ligowy "każdy z każdym" (round-robin) metodą okręgu.
/// Każda para drużyn spotyka się dokładnie raz. Przy nieparzystej liczbie drużyn w każdej kolejce jedna z nich pauzuje.
/// Wynik zależy tylko od kolejności podanych drużyn (bez losowania), więc kolejność ustala wołający.
/// </summary>
public static class RoundRobinScheduler
{
    /// <summary>
    /// Buduje terminarz dla podanych drużyn: n - 1 kolejek dla parzystej liczby drużyn i n kolejek dla nieparzystej.
    /// </summary>
    /// <param name="teamIds">Identyfikatory drużyn, co najmniej dwie i bez powtórzeń.</param>
    public static IReadOnlyList<RoundRobinRound> Generate(IReadOnlyList<Guid> teamIds)
    {
        ArgumentNullException.ThrowIfNull(teamIds);

        if (teamIds.Count < 2)
        {
            throw new ArgumentException("Terminarz ligowy wymaga co najmniej 2 drużyn.", nameof(teamIds));
        }

        if (teamIds.Distinct().Count() != teamIds.Count)
        {
            throw new ArgumentException("Drużyny w terminarzu nie mogą się powtarzać.", nameof(teamIds));
        }

        // Pierwsza pozycja jest stała, pozostałe krążą. Przy nieparzystej liczbie drużyn stałą pozycję zajmuje pusty
        // slot (pauza), więc każda drużyna pauzuje dokładnie raz, gdy trafi naprzeciw niego.
        var slots = teamIds.Select(id => (Guid?)id).ToList();
        if (slots.Count % 2 == 1)
        {
            slots.Insert(0, null);
        }

        var slotCount = slots.Count;
        var rounds = new List<RoundRobinRound>(slotCount - 1);

        for (var roundIndex = 0; roundIndex < slotCount - 1; roundIndex++)
        {
            var pairings = new List<RoundRobinPairing>(slotCount / 2);
            Guid? restingTeamId = null;

            for (var board = 0; board < slotCount / 2; board++)
            {
                var first = slots[board];
                var second = slots[slotCount - 1 - board];

                if (first is null || second is null)
                {
                    restingTeamId = first ?? second;
                    continue;
                }

                // Gospodarzem jest drużyna z lewej strony stolika, a przy stałej pozycji gospodarz zmienia się co kolejkę.
                // Dzięki temu liczba meczów u siebie i na wyjeździe każdej drużyny różni się najwyżej o 1.
                var firstIsHome = board > 0 || roundIndex % 2 == 0;
                pairings.Add(firstIsHome
                    ? new RoundRobinPairing(first.Value, second.Value)
                    : new RoundRobinPairing(second.Value, first.Value));
            }

            rounds.Add(new RoundRobinRound(roundIndex + 1, pairings, restingTeamId));

            // Obrót: ostatni slot trafia na drugą pozycję, pierwsza zostaje na miejscu
            var last = slots[^1];
            slots.RemoveAt(slotCount - 1);
            slots.Insert(1, last);
        }

        return rounds;
    }
}
