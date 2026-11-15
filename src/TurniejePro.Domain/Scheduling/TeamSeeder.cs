using System.Security.Cryptography;
using TurniejePro.Domain.Enums;
using TurniejePro.Domain.Exceptions;

namespace TurniejePro.Domain.Scheduling;

/// <summary>
/// Ustala kolejność rozstawienia drużyn przed wygenerowaniem terminarza: pierwsza drużyna w wyniku jest rozstawiona
/// najwyżej. Kolejność wynika wyłącznie z wybranego trybu, nie z kolejności, w jakiej baza zwróciła drużyny.
/// </summary>
public static class TeamSeeder
{
    /// <summary>
    /// Zwraca identyfikatory drużyn w kolejności rozstawienia.
    /// </summary>
    /// <param name="teams">Drużyny do rozstawienia (zaakceptowane w turnieju).</param>
    /// <param name="mode">Tryb: stała kolejność zgłoszeń, losowanie albo ręczne rozstawienie.</param>
    /// <param name="manualOrder">Kolejność podana przez organizatora, wymagana w trybie ręcznym: dokładnie wszystkie drużyny, każda raz.</param>
    /// <param name="nextIndex">Losowanie liczby z zakresu od 0 do podanej wartości (wyłącznie). Domyślnie generator
    /// kryptograficzny; podmieniany w testach, żeby losowanie było powtarzalne.</param>
    public static IReadOnlyList<Guid> Seed(
        IReadOnlyCollection<Team> teams,
        SeedingMode mode,
        IReadOnlyList<Guid>? manualOrder = null,
        Func<int, int>? nextIndex = null)
    {
        ArgumentNullException.ThrowIfNull(teams);

        // Stała kolejność: czas zgłoszenia, a przy identycznym czasie identyfikator, żeby wynik był jednoznaczny
        var byRegistration = teams.OrderBy(team => team.RegisteredAt).ThenBy(team => team.Id).Select(team => team.Id).ToList();

        return mode switch
        {
            SeedingMode.Registration => byRegistration,
            SeedingMode.Random => Shuffle(byRegistration, nextIndex ?? RandomNumberGenerator.GetInt32),
            SeedingMode.Manual => ValidateManualOrder(byRegistration, manualOrder),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Nieznany sposób rozstawienia.")
        };
    }

    /// <summary>Tasowanie Fishera-Yatesa: od końca listy zamienia każdy element z losowym spośród stojących przed nim (i nim samym).</summary>
    private static List<Guid> Shuffle(List<Guid> ordered, Func<int, int> nextIndex)
    {
        var shuffled = new List<Guid>(ordered);

        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = nextIndex(i + 1);

            if (j < 0 || j > i)
            {
                throw new InvalidOperationException($"Generator zwrócił {j}, a oczekiwano liczby od 0 do {i}.");
            }

            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return shuffled;
    }

    private static List<Guid> ValidateManualOrder(List<Guid> accepted, IReadOnlyList<Guid>? manualOrder)
    {
        if (manualOrder is null)
        {
            throw new BusinessRuleException("Ręczne rozstawienie wymaga listy drużyn w kolejności rozstawienia.");
        }

        if (manualOrder.Distinct().Count() != manualOrder.Count)
        {
            throw new BusinessRuleException("Ręczne rozstawienie nie może zawierać tej samej drużyny więcej niż raz.");
        }

        var acceptedIds = accepted.ToHashSet();

        if (manualOrder.Any(id => !acceptedIds.Contains(id)))
        {
            throw new BusinessRuleException("Ręczne rozstawienie zawiera drużynę, która nie jest zaakceptowana w tym turnieju.");
        }

        if (manualOrder.Count != acceptedIds.Count)
        {
            throw new BusinessRuleException($"Ręczne rozstawienie musi obejmować wszystkie zaakceptowane drużyny ({acceptedIds.Count}).");
        }

        return manualOrder.ToList();
    }
}
