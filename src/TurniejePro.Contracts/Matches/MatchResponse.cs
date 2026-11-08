namespace TurniejePro.Contracts.Matches;

/// <summary>
/// Mecz w terminarzu turnieju.
/// </summary>
/// <param name="Id">Identyfikator meczu.</param>
/// <param name="Round">Numer rundy (od 1).</param>
/// <param name="Position">Numer meczu w rundzie (od 1).</param>
/// <param name="HomeTeam">Gospodarz albo null, gdy jeszcze nie wiadomo, kto zagra.</param>
/// <param name="AwayTeam">Gość albo null, gdy jeszcze nie wiadomo, kto zagra, lub gdy to wolny los.</param>
/// <param name="HomeScore">Wynik gospodarza albo null, gdy mecz nie został rozegrany.</param>
/// <param name="AwayScore">Wynik gościa albo null, gdy mecz nie został rozegrany.</param>
/// <param name="Status">Status meczu.</param>
/// <param name="IsBye">Wolny los: gospodarz przechodzi dalej bez gry.</param>
/// <param name="ScheduledAt">Termin meczu (UTC), jeśli ustalony.</param>
public sealed record MatchResponse(
    Guid Id,
    int Round,
    int Position,
    MatchTeamResponse? HomeTeam,
    MatchTeamResponse? AwayTeam,
    int? HomeScore,
    int? AwayScore,
    MatchStatus Status,
    bool IsBye,
    DateTimeOffset? ScheduledAt);
