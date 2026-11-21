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

        // Wyniki wpisujemy dopiero w rozpoczętym turnieju (także poprawki po zakończeniu). Bez tego wyniki wpisane
        // w szkicu lub w zapisach nigdy nie zakończyłyby turnieju (KAN-290) -> 400
        if (!tournament.IsStarted)
        {
            throw new BusinessRuleException("Wyniki można wpisywać dopiero po rozpoczęciu turnieju (po wygenerowaniu terminarza).");
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

        // Mecz drabinki (pucharu albo fazy pucharowej) musi mieć zwycięzcę. Mecze grup i ligi podlegają zasadom punktacji.
        var isKnockout = tournament.Format == TournamentFormat.Cup
            || (tournament.Format == TournamentFormat.GroupsAndCup && !match.IsGroupStage);

        // 5. Remis -> 400. W drabince remisu nie ma, bo drabinka potrzebuje zwycięzcy; w lidze i grupach remis może być
        // wyłączony w zasadach punktacji (ScoringRules.AllowDraws).
        if (homeScore == awayScore)
        {
            if (isKnockout)
            {
                throw new BusinessRuleException("W fazie pucharowej mecz nie może zakończyć się remisowo.");
            }

            if (!tournament.ScoringRules.AllowDraws)
            {
                throw new BusinessRuleException("Zasady punktacji tego turnieju nie dopuszczają remisów.");
            }
        }

        Match? nextMatch = null;

        // Drabinka to mecze pucharowe; mecze fazy grupowej do niej nie należą.
        var knockoutMatches = tournament.Format == TournamentFormat.GroupsAndCup
            ? matches.Where(m => !m.IsGroupStage).ToList()
            : matches;

        // 6. Faza pucharowa: sprawdzenie meczu następnej rundy -> 409. Miejsce zwycięzcy wskazuje jedna reguła awansu.
        if (isKnockout)
        {
            var slot = KnockoutBracketAdvancer.GetNextSlot(match.Round, match.Position);

            nextMatch = knockoutMatches.FirstOrDefault(m => m.Round == slot.RoundNumber && m.Position == slot.Position);

            if (nextMatch is not null && nextMatch.Status != MatchStatus.Scheduled)
            {
                throw new MatchResultConflictException("Mecz następnej rundy został już rozpoczęty lub rozegrany.");
            }
        }

        // Drabinka sprzed nowego wyniku: awans (także korekta wyniku) liczy KnockoutBracketAdvancer
        var bracket = nextMatch is null ? null : MatchBracketMapper.ToBracket(knockoutMatches);

        // Ustawienie wyniku meczu
        match.SetResult(homeScore, awayScore);

        // Awans zwycięzcy w drabince pucharowej
        if (bracket is not null)
        {
            var winnerId = homeScore > awayScore ? match.HomeTeamId : match.AwayTeamId;

            var advanced = KnockoutBracketAdvancer.AdvanceWinner(bracket, match.Round, match.Position, winnerId!.Value);
            MatchBracketMapper.ApplyTeams(advanced, knockoutMatches);
        }

        // Ostatni mecz fazy grupowej: tworzymy drabinkę z najlepszych drużyn każdej grupy.
        var knockoutCreated = false;
        if (tournament.Format == TournamentFormat.GroupsAndCup && match.IsGroupStage)
        {
            knockoutCreated = await CreateKnockoutIfGroupStageFinishedAsync(tournament, matches, cancellationToken);
        }

        // Turniej kończy się, gdy zagrano wszystkie mecze. Po utworzeniu drabinki są w niej jeszcze nierozegrane mecze.
        if (!knockoutCreated)
        {
            tournament.CompleteIfAllMatchesFinished(matches);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return match;
    }

    private async Task<bool> CreateKnockoutIfGroupStageFinishedAsync(
        Tournament tournament,
        IReadOnlyList<Match> matches,
        CancellationToken cancellationToken)
    {
        // Drabinka powstaje raz: dopiero gdy wszystkie mecze grupowe są zakończone i jeszcze nie istnieje.
        var groupMatches = matches.Where(m => m.IsGroupStage).ToList();
        if (groupMatches.Any(m => m.Status != MatchStatus.Finished) || matches.Any(m => !m.IsGroupStage))
        {
            return false;
        }

        var teams = await _unitOfWork.Teams.GetByTournamentIdAndStatusAsync(
            tournament.Id,
            TeamRegistrationStatus.Accepted,
            cancellationToken);

        var firstKnockoutRound = groupMatches.Max(m => m.Round) + 1;
        var knockout = GroupStage.BuildKnockout(tournament.Id, teams, groupMatches, tournament.ScoringRules, firstKnockoutRound);

        foreach (var knockoutMatch in knockout)
        {
            await _unitOfWork.Matches.AddAsync(knockoutMatch, cancellationToken);
        }

        return true;
    }
}
