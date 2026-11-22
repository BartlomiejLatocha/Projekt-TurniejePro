using System.Globalization;
using TurniejePro.Contracts.Matches;
using TurniejePro.Contracts.Standings;

namespace TurniejePro.Api.Exports;

internal static class TournamentExportTables
{
    internal static IReadOnlyList<string> ScheduleHeaders { get; } =
        ["Runda / kolejka", "Nr", "Grupa", "Gospodarz", "Wynik", "Gość", "Status"];

    internal static IReadOnlyList<string> StandingsHeaders { get; } =
        ["Grupa", "Miejsce", "Drużyna", "M", "W", "R", "P", "Bramki", "Bilans", "Pkt"];

    internal static IEnumerable<string[]> ScheduleRows(ScheduleResponse schedule) =>
        schedule.Rounds.SelectMany(round => round.Matches.Select(match => new[]
        {
            Number(round.Round),
            Number(match.Position),
            GroupLetter(match.Group),
            match.HomeTeam?.Name ?? "Do ustalenia",
            MatchScore(match),
            match.IsBye ? "Wolny los" : match.AwayTeam?.Name ?? "Do ustalenia",
            MatchStatusName(match.Status)
        }));

    internal static IEnumerable<string[]> StandingsRows(StandingsResponse standings) =>
        standings.Rows.Select(row => new[]
        {
            GroupLetter(row.Group),
            Number(row.Place),
            row.TeamName,
            Number(row.Played),
            Number(row.Won),
            Number(row.Drawn),
            Number(row.Lost),
            FormattableString.Invariant($"{row.GoalsFor}:{row.GoalsAgainst}"),
            row.GoalDifference.ToString("+0;-0;0", CultureInfo.InvariantCulture),
            Number(row.Points)
        });

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string GroupLetter(int? group) =>
        group is { } number ? ((char)('A' + number - 1)).ToString() : string.Empty;

    private static string MatchScore(MatchResponse match) =>
        match.IsBye || match.HomeScore is null || match.AwayScore is null
            ? "-"
            : FormattableString.Invariant($"{match.HomeScore} : {match.AwayScore}");

    private static string MatchStatusName(MatchStatus status) => status switch
    {
        MatchStatus.Scheduled => "Zaplanowany",
        MatchStatus.InProgress => "W toku",
        MatchStatus.Finished => "Zakończony",
        MatchStatus.Cancelled => "Anulowany",
        _ => status.ToString()
    };
}
