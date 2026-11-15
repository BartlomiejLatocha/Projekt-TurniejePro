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

        // 5. Remis w fazie pucharowej -> 400
        if (tournament.Format == TournamentFormat.Cup && homeScore == awayScore)
        {
            throw new BusinessRuleException("W fazie pucharowej mecz nie może zakończyć się remisowo.");
        }

        Match? nextMatch = null;

        // 6. Faza pucharowa: sprawdzenie meczu następnej rundy -> 409. Miejsce zwycięzcy wskazuje jedna reguła awansu.
        if (tournament.Format == TournamentFormat.Cup)
        {
            var slot = KnockoutBracketAdvancer.GetNextSlot(match.Round, match.Position);

            nextMatch = matches.FirstOrDefault(m => m.Round == slot.RoundNumber && m.Position == slot.Position);

            if (nextMatch is not null && nextMatch.Status != MatchStatus.Scheduled)
            {
                throw new MatchResultConflictException("Mecz następnej rundy został już rozpoczęty lub rozegrany.");
            }
        }

        // Drabinka sprzed nowego wyniku: awans (także korekta wyniku) liczy KnockoutBracketAdvancer
        var bracket = nextMatch is null ? null : MatchBracketMapper.ToBracket(matches);

        // Ustawienie wyniku meczu
        match.SetResult(homeScore, awayScore);

        // Awans zwycięzcy w drabince pucharowej
        if (bracket is not null)
        {
            var winnerId = homeScore > awayScore ? match.HomeTeamId : match.AwayTeamId;

            var advanced = KnockoutBracketAdvancer.AdvanceWinner(bracket, match.Round, match.Position, winnerId!.Value);
            MatchBracketMapper.ApplyTeams(advanced, matches);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return match;
    }
}
