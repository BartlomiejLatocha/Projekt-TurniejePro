using TurniejePro.Domain.Enums;

namespace TurniejePro.Domain.Standings;

/// <summary>
/// Liczy tabelę ligową z rozegranych meczów. Tabela jest funkcją meczów, więc po poprawieniu wyniku wystarczy
/// policzyć ją od nowa.
/// </summary>
public static class StandingsCalculator
{
    /// <summary>Punkty za wygraną.</summary>
    public const int PointsForWin = 3;

    /// <summary>Punkty za remis.</summary>
    public const int PointsForDraw = 1;

    /// <summary>
    /// Buduje tabelę dla podanych drużyn. Liczą się tylko zakończone mecze z wynikiem, w których grają dwie drużyny
    /// z tabeli. Pozostałe mecze (zaplanowane, w toku, odwołane, z nieznaną drużyną) są pomijane.
    /// Kolejność wierszy rozstrzygają kolejno: punkty, bilans bramek, bramki zdobyte, punkty z meczów bezpośrednich
    /// pomiędzy drużynami remisującymi do tego miejsca, a na końcu kolejność drużyn na liście wejściowej.
    /// Drużyny, których nie rozstrzygają żadne z tych zasad, mają to samo miejsce (np. 1, 1, 3).
    /// </summary>
    /// <param name="teamIds">Drużyny w tabeli, bez powtórzeń. Kolejność służy tylko do uporządkowania remisów w pełni równych.</param>
    /// <param name="matches">Mecze turnieju.</param>
    public static IReadOnlyList<StandingsRow> Calculate(IReadOnlyList<Guid> teamIds, IEnumerable<Match> matches)
    {
        ArgumentNullException.ThrowIfNull(teamIds);
        ArgumentNullException.ThrowIfNull(matches);

        if (teamIds.Distinct().Count() != teamIds.Count)
        {
            throw new ArgumentException("Drużyny w tabeli nie mogą się powtarzać.", nameof(teamIds));
        }

        var tallies = teamIds.ToDictionary(id => id, _ => new Tally());
        var counted = matches.Select(ToResult).OfType<MatchResult>().Where(result => tallies.ContainsKey(result.HomeTeamId) && tallies.ContainsKey(result.AwayTeamId)).ToList();

        foreach (var result in counted)
        {
            tallies[result.HomeTeamId].Add(result.HomeScore, result.AwayScore);
            tallies[result.AwayTeamId].Add(result.AwayScore, result.HomeScore);
        }

        var entries = teamIds
            .Select((teamId, index) => (TeamId: teamId, Index: index, Tally: tallies[teamId]))
            .ToList();

        var rows = new List<StandingsRow>(entries.Count);
        var groups = entries
            .GroupBy(entry => (entry.Tally.Points, entry.Tally.GoalDifference, entry.Tally.GoalsFor))
            .OrderByDescending(group => group.Key.Points)
            .ThenByDescending(group => group.Key.GoalDifference)
            .ThenByDescending(group => group.Key.GoalsFor);

        foreach (var group in groups)
        {
            var members = group.Select(entry => entry.TeamId).ToHashSet();
            var headToHead = group.ToDictionary(entry => entry.TeamId, entry => HeadToHeadPoints(entry.TeamId, members, counted));

            var previousHeadToHead = 0;
            var previousPlace = 0;

            foreach (var entry in group.OrderByDescending(entry => headToHead[entry.TeamId]).ThenBy(entry => entry.Index))
            {
                var sharesPlaceWithPrevious = previousPlace > 0 && headToHead[entry.TeamId] == previousHeadToHead;
                var place = sharesPlaceWithPrevious ? previousPlace : rows.Count + 1;

                rows.Add(new StandingsRow(
                    place,
                    entry.TeamId,
                    entry.Tally.Played,
                    entry.Tally.Won,
                    entry.Tally.Drawn,
                    entry.Tally.Lost,
                    entry.Tally.GoalsFor,
                    entry.Tally.GoalsAgainst,
                    entry.Tally.Points));

                previousHeadToHead = headToHead[entry.TeamId];
                previousPlace = place;
            }
        }

        return rows;
    }

    private static MatchResult? ToResult(Match match) =>
        match is { Status: MatchStatus.Finished, HomeScore: { } homeScore, AwayScore: { } awayScore, HomeTeamId: { } homeTeamId, AwayTeamId: { } awayTeamId }
        && homeTeamId != awayTeamId
            ? new MatchResult(homeTeamId, awayTeamId, homeScore, awayScore)
            : null;

    /// <summary>Punkty zdobyte przez drużynę wyłącznie w meczach z pozostałymi drużynami z grupy remisującej.</summary>
    private static int HeadToHeadPoints(Guid teamId, HashSet<Guid> group, List<MatchResult> results)
    {
        var points = 0;

        foreach (var result in results.Where(result => group.Contains(result.HomeTeamId) && group.Contains(result.AwayTeamId)))
        {
            if (result.HomeTeamId == teamId)
            {
                points += PointsFor(result.HomeScore, result.AwayScore);
            }
            else if (result.AwayTeamId == teamId)
            {
                points += PointsFor(result.AwayScore, result.HomeScore);
            }
        }

        return points;
    }

    private static int PointsFor(int goalsFor, int goalsAgainst) =>
        goalsFor > goalsAgainst ? PointsForWin : goalsFor == goalsAgainst ? PointsForDraw : 0;

    private sealed record MatchResult(Guid HomeTeamId, Guid AwayTeamId, int HomeScore, int AwayScore);

    private sealed class Tally
    {
        public int Played { get; private set; }

        public int Won { get; private set; }

        public int Drawn { get; private set; }

        public int Lost { get; private set; }

        public int GoalsFor { get; private set; }

        public int GoalsAgainst { get; private set; }

        public int Points => (Won * PointsForWin) + (Drawn * PointsForDraw);

        public int GoalDifference => GoalsFor - GoalsAgainst;

        public void Add(int goalsFor, int goalsAgainst)
        {
            Played++;
            GoalsFor += goalsFor;
            GoalsAgainst += goalsAgainst;

            if (goalsFor > goalsAgainst)
            {
                Won++;
            }
            else if (goalsFor == goalsAgainst)
            {
                Drawn++;
            }
            else
            {
                Lost++;
            }
        }
    }
}
