using TurniejePro.Contracts.Matches;
using TurniejePro.Contracts.Standings;

namespace TurniejePro.Api.Exports;

/// <summary>Dane raportu. Pusty terminarz oznacza brak meczów, a null w tabeli oznacza format pucharowy.</summary>
public sealed record TournamentExportData(
    string TournamentName,
    ScheduleResponse Schedule,
    StandingsResponse? Standings);
