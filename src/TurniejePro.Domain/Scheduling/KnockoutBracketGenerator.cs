namespace TurniejePro.Domain.Scheduling;

/// <summary>
/// Generuje drabinkę pucharową (eliminacja po jednej porażce) z rozstawieniem drużyn.
/// Gdy liczba drużyn nie jest potęgą dwójki, drabinka jest uzupełniana do najbliższej potęgi, a brakujące miejsca
/// to wolne losy, które dostają najwyżej rozstawione drużyny. Wynik zależy tylko od kolejności podanych drużyn.
/// </summary>
public static class KnockoutBracketGenerator
{
    /// <summary>
    /// Buduje drabinkę. Pierwsza runda zawiera mecze i wolne losy, a drużyny z wolnym losem są od razu wpisane
    /// do drugiej rundy. Pozostałe miejsca w kolejnych rundach czekają na zwycięzców.
    /// </summary>
    /// <param name="seededTeamIds">Identyfikatory drużyn w kolejności rozstawienia (pierwsza to najwyżej rozstawiona),
    /// co najmniej dwie i bez powtórzeń.</param>
    public static IReadOnlyList<BracketRound> Generate(IReadOnlyList<Guid> seededTeamIds)
    {
        ArgumentNullException.ThrowIfNull(seededTeamIds);

        if (seededTeamIds.Count < 2)
        {
            throw new ArgumentException("Drabinka wymaga co najmniej 2 drużyn.", nameof(seededTeamIds));
        }

        if (seededTeamIds.Distinct().Count() != seededTeamIds.Count)
        {
            throw new ArgumentException("Drużyny w drabince nie mogą się powtarzać.", nameof(seededTeamIds));
        }

        var bracketSize = 2;
        while (bracketSize < seededTeamIds.Count)
        {
            bracketSize *= 2;
        }

        var seedOrder = CreateSeedOrder(bracketSize);
        var rounds = new List<BracketRound> { CreateFirstRound(seededTeamIds, seedOrder) };

        while (rounds[^1].Matches.Count > 1)
        {
            rounds.Add(CreateNextRound(rounds[^1]));
        }

        return rounds;
    }

    private static BracketRound CreateFirstRound(IReadOnlyList<Guid> seededTeamIds, List<int> seedOrder)
    {
        var matches = new List<BracketMatch>(seedOrder.Count / 2);

        for (var i = 0; i < seedOrder.Count / 2; i++)
        {
            // W każdej parze pierwszy jest wyżej rozstawiony, więc istnieje zawsze. Brak rywala oznacza wolny los.
            var home = TeamForSeed(seededTeamIds, seedOrder[2 * i]);
            var away = TeamForSeed(seededTeamIds, seedOrder[(2 * i) + 1]);
            var isBye = away is null;

            matches.Add(new BracketMatch(i + 1, home, away, isBye, isBye ? home : null));
        }

        return new BracketRound(1, matches);
    }

    private static BracketRound CreateNextRound(BracketRound previous)
    {
        var matches = new List<BracketMatch>(previous.Matches.Count / 2);

        for (var i = 0; i < previous.Matches.Count / 2; i++)
        {
            // Zwycięzcy dwóch kolejnych meczów trafiają do jednego meczu: z nieparzystego jako gospodarz, z parzystego jako gość
            matches.Add(new BracketMatch(
                i + 1,
                previous.Matches[2 * i].WinnerId,
                previous.Matches[(2 * i) + 1].WinnerId,
                IsBye: false,
                WinnerId: null));
        }

        return new BracketRound(previous.Number + 1, matches);
    }

    private static Guid? TeamForSeed(IReadOnlyList<Guid> seededTeamIds, int seed) =>
        seed <= seededTeamIds.Count ? seededTeamIds[seed - 1] : null;

    /// <summary>
    /// Kolejność rozstawienia w drabince, np. dla 8: 1, 8, 4, 5, 2, 7, 3, 6 (pary: 1-8, 4-5, 2-7, 3-6).
    /// Dzięki temu dwie najwyżej rozstawione drużyny mogą spotkać się dopiero w finale.
    /// </summary>
    private static List<int> CreateSeedOrder(int bracketSize)
    {
        var order = new List<int> { 1 };

        for (var size = 1; size < bracketSize; size *= 2)
        {
            var newSize = size * 2;
            order = order.SelectMany(seed => new[] { seed, newSize + 1 - seed }).ToList();
        }

        return order;
    }
}
