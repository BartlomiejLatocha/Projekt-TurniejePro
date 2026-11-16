using TurniejePro.Domain.Standings;

namespace TurniejePro.Domain.Scheduling;

/// <summary>
/// Faza grupowa turnieju: podział drużyn na grupy, mecze każdy z każdym w grupie oraz drabinka, do której awansują
/// dwie najlepsze drużyny z każdej grupy.
/// </summary>
public static class GroupStage
{
    /// <summary>Najmniej drużyn w grupie.</summary>
    public const int MinTeamsPerGroup = 3;

    /// <summary>Drużyn awansujących z każdej grupy do drabinki.</summary>
    public const int AdvancingPerGroup = 2;

    /// <summary>
    /// Liczba grup dla danej liczby drużyn: największa potęga dwójki (co najmniej 2), w której każda grupa ma co najmniej
    /// <see cref="MinTeamsPerGroup"/> drużyn. Dzięki potędze dwójki drabinka po fazie grupowej nie potrzebuje wolnych losów.
    /// Zwraca 0, gdy drużyn jest za mało.
    /// </summary>
    public static int GroupCountFor(int teamCount)
    {
        var groups = 0;
        for (var candidate = 2; teamCount / candidate >= MinTeamsPerGroup; candidate *= 2)
        {
            groups = candidate;
        }

        return groups;
    }

    /// <summary>
    /// Rozdziela drużyny (w kolejności rozstawienia) na grupy metodą "wężyka": 1. drużyna do grupy 1, 2. do grupy 2 itd.,
    /// a w kolejnym przebiegu w odwrotnej kolejności. Grupy są równoliczne (różnią się najwyżej o jedną drużynę).
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<Guid>> Distribute(IReadOnlyList<Guid> seededTeamIds, int groupCount)
    {
        ArgumentNullException.ThrowIfNull(seededTeamIds);
        if (groupCount < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(groupCount), "Faza grupowa wymaga co najmniej 2 grup.");
        }

        var groups = Enumerable.Range(0, groupCount).Select(_ => new List<Guid>()).ToList();
        for (var index = 0; index < seededTeamIds.Count; index++)
        {
            var row = index / groupCount;
            var column = index % groupCount;
            var group = row % 2 == 0 ? column : groupCount - 1 - column;
            groups[group].Add(seededTeamIds[index]);
        }

        return groups;
    }

    /// <summary>
    /// Buduje mecze drabinki po zakończeniu fazy grupowej. Pierwsza runda łączy zwycięzcę grupy i (odwrotnie) drugą
    /// drużynę innej grupy: zwycięzca grupy i z jest w parze z drugim miejscem grupy <c>g - 1 - i</c>. Kolejne rundy są puste
    /// i zapełnią się przy wpisywaniu wyników (awans zwycięzców).
    /// </summary>
    /// <param name="tournamentId">Turniej.</param>
    /// <param name="teams">Zaakceptowane drużyny z przypisanym numerem grupy.</param>
    /// <param name="groupMatches">Mecze fazy grupowej.</param>
    /// <param name="scoringRules">Punktacja turnieju (potrzebna do tabeli grup).</param>
    /// <param name="firstKnockoutRound">Numer pierwszej rundy drabinki (po ostatniej rundzie grupowej).</param>
    public static IReadOnlyList<Match> BuildKnockout(
        Guid tournamentId,
        IReadOnlyList<Team> teams,
        IReadOnlyList<Match> groupMatches,
        ScoringRules scoringRules,
        int firstKnockoutRound)
    {
        ArgumentNullException.ThrowIfNull(teams);
        ArgumentNullException.ThrowIfNull(groupMatches);

        var groupNumbers = teams.Where(t => t.GroupNumber is not null).Select(t => t.GroupNumber!.Value)
            .Distinct().OrderBy(n => n).ToList();
        var groupCount = groupNumbers.Count;
        if (groupCount < 2 || (groupCount & (groupCount - 1)) != 0)
        {
            throw new InvalidOperationException("Drabinka po fazie grupowej wymaga potęgi dwójki grup (co najmniej 2).");
        }

        // Pierwsza dwójka każdej grupy wg tabeli grupy (zwycięzca i drugie miejsce).
        var winners = new List<Guid>();
        var runnersUp = new List<Guid>();
        foreach (var number in groupNumbers)
        {
            var groupTeamIds = teams.Where(t => t.GroupNumber == number).Select(t => t.Id).ToList();
            var matchesOfGroup = groupMatches
                .Where(m => m.HomeTeamId is { } h && groupTeamIds.Contains(h))
                .ToList();

            var table = StandingsCalculator.Calculate(groupTeamIds, matchesOfGroup, scoringRules);
            if (table.Count < AdvancingPerGroup)
            {
                throw new InvalidOperationException($"Grupa {number} ma za mało drużyn, by awansować dwie.");
            }

            winners.Add(table[0].TeamId);
            runnersUp.Add(table[1].TeamId);
        }

        var matches = new List<Match>();
        var pairs = groupCount;
        for (var i = 0; i < groupCount; i++)
        {
            matches.Add(new Match(
                tournamentId,
                firstKnockoutRound,
                i + 1,
                winners[i],
                runnersUp[groupCount - 1 - i]));
        }

        // Kolejne rundy: liczba meczów maleje o połowę aż do finału.
        var roundNumber = firstKnockoutRound + 1;
        for (var size = pairs / 2; size >= 1; size /= 2, roundNumber++)
        {
            for (var position = 1; position <= size; position++)
            {
                matches.Add(new Match(tournamentId, roundNumber, position));
            }
        }

        foreach (var match in matches)
        {
            match.IsGroupStage = false;
        }

        return matches;
    }
}
