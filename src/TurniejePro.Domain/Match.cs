using TurniejePro.Domain.Enums;

namespace TurniejePro.Domain;

/// <summary>
/// Encja reprezentująca pojedynczy mecz turniejowy.
/// </summary>
public class Match
{
    public Guid Id { get; set; }
    public Guid TournamentId { get; set; }
    public Tournament? Tournament { get; set; }
    public int Round { get; set; }
    public Guid? HomeTeamId { get; set; }
    public Team? HomeTeam { get; set; }
    public Guid? AwayTeamId { get; set; }
    public Team? AwayTeam { get; set; }
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public MatchStatus Status { get; private set; } = MatchStatus.Scheduled;
    public DateTimeOffset? ScheduledAt { get; set; }

    /// <summary>
    /// Konstruktor dla EF Core.
    /// </summary>
    protected Match()
    {
    }

    /// <summary>
    /// Tworzy nowy mecz w terminarzu turnieju.
    /// </summary>
    public Match(
        Guid tournamentId,
        int round,
        Guid? homeTeamId = null,
        Guid? awayTeamId = null,
        DateTimeOffset? scheduledAt = null)
    {
        if (round < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(round), "Numer rundy musi być większy lub równy 1.");
        }

        Id = Guid.NewGuid();
        TournamentId = tournamentId;
        Round = round;
        HomeTeamId = homeTeamId;
        AwayTeamId = awayTeamId;
        ScheduledAt = scheduledAt;
        Status = MatchStatus.Scheduled;
    }

    /// <summary>
    /// Oznacza mecz jako rozpoczęty (w toku).
    /// </summary>
    public void StartMatch()
    {
        if (Status != MatchStatus.Scheduled)
        {
            throw new InvalidOperationException("Tylko zaplanowany mecz można rozpocząć.");
        }

        Status = MatchStatus.InProgress;
    }

    /// <summary>
    /// Ustawia końcowy wynik meczu.
    /// </summary>
    public void SetResult(int homeScore, int awayScore)
    {
        if (homeScore < 0 || awayScore < 0)
        {
            throw new ArgumentException("Wynik meczu nie może być ujemny.");
        }

        HomeScore = homeScore;
        AwayScore = awayScore;
        Status = MatchStatus.Finished;
    }

    /// <summary>
    /// Anuluje zaplanowany mecz.
    /// </summary>
    public void Cancel() => Status = MatchStatus.Cancelled;
}
