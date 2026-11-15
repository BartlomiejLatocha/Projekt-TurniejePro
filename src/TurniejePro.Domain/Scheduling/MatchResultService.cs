using TurniejePro.Domain.Enums;
using TurniejePro.Domain.Exceptions;
using TurniejePro.Domain.Repositories;

namespace TurniejePro.Domain.Scheduling;

public class MatchResultService
{
    private readonly IUnitOfWork _unitOfWork;

    public MatchResultService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Match> SetResultAsync(
        Guid tournamentId,
        Guid matchId,
        int homeScore,
        int awayScore,
        CancellationToken cancellationToken = default)
    {
        // 1. Walidacja istnienia turnieju -> 404
        var tournament = await _unitOfWork.Tournaments.GetByIdAsync(tournamentId, cancellationToken);
        if (tournament is null)
        {
            throw new NotFoundException($"Nie odnaleziono turnieju o ID '{tournamentId}'.");
        }

        // Pobranie meczów turnieju ze śledzeniem (EF Core Tracker) i drużynami
        var matches = await _unitOfWork.Matches.GetByTournamentIdAsync(tournamentId, cancellationToken);

        // 2. Walidacja istnienia meczu w tym turnieju -> 404
        var match = matches.FirstOrDefault(m => m.Id == matchId);
        if (match is null || match.TournamentId != tournamentId)
        {
            throw new NotFoundException($"Mecz o ID '{matchId}' nie istnieje w turnieju '{tournamentId}'.");
        }

        // 3. Sprawdzenie typu meczu: wolny los lub anulowany -> 409
        if (match.IsBye || match.Status == MatchStatus.Cancelled)
        {
            throw new MatchResultConflictException("Nie można wprowadzić wyniku dla wolnego losu lub anulowanego meczu.");
        }

        // 4. Brak którejś drużyny w meczu -> 409
        if (match.HomeTeamId is null || match.AwayTeamId is null)
        {
            throw new MatchResultConflictException("Obie drużyny muszą być wyznaczone, aby wprowadzić wynik meczu.");
        }

        // 5. Remis -> 400. W pucharze remisu nie ma, bo drabinka potrzebuje zwycięzcy, a w pozostałych turniejach remis
        // może być wyłączony w zasadach punktacji (ScoringRules.AllowDraws).
        if (homeScore == awayScore)
        {
            if (tournament.Format == TournamentFormat.Cup)
            {
                throw new BusinessRuleException("W fazie pucharowej mecz nie może zakończyć się remisowo.");
            }

            if (!tournament.ScoringRules.AllowDraws)
            {
                throw new BusinessRuleException("Zasady punktacji tego turnieju nie dopuszczają remisów.");
            }
        }

        Match? nextMatch = null;

        // 6. Faza pucharowa: sprawdzenie meczu następnej rundy -> 409
        if (tournament.Format == TournamentFormat.Cup)
        {
            var nextRound = match.Round + 1;
            var nextPosition = (match.Position + 1) / 2;

            nextMatch = matches.FirstOrDefault(m => m.Round == nextRound && m.Position == nextPosition);

            if (nextMatch is not null && nextMatch.Status != MatchStatus.Scheduled)
            {
                throw new MatchResultConflictException("Mecz następnej rundy został już rozpoczęty lub rozegrany.");
            }
        }

        // Ustawienie wyniku meczu
        match.SetResult(homeScore, awayScore);

        // Awans zwycięzcy w drabince pucharowej
        if (nextMatch is not null)
        {
            var winnerId = homeScore > awayScore ? match.HomeTeamId : match.AwayTeamId;

            if (match.Position % 2 == 1)
            {
                nextMatch.HomeTeamId = winnerId; // Pozycja nieparzysta -> Gospodarz
            }
            else
            {
                nextMatch.AwayTeamId = winnerId; // Pozycja parzysta -> Gość
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return match;
    }
}
