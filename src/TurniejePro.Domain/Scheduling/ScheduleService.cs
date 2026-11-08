using TurniejePro.Domain.Enums;
using TurniejePro.Domain.Repositories;

namespace TurniejePro.Domain.Scheduling;

public class ScheduleService
{
    private readonly IUnitOfWork _unitOfWork;

    public ScheduleService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<Match>> GenerateScheduleAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        // 1. Sprawdzenie, czy terminarz już istnieje
        var hasSchedule = await _unitOfWork.Matches.HasScheduleAsync(tournamentId, cancellationToken);
        if (hasSchedule)
        {
            throw new ScheduleAlreadyExistsException($"Terminarz dla turnieju '{tournamentId}' już istnieje.");
        }

        // 2. Pobranie turnieju
        var tournament = await _unitOfWork.Tournaments.GetByIdAsync(tournamentId, cancellationToken);
        if (tournament is null)
        {
            throw new KeyNotFoundException($"Nie odnaleziono turnieju o ID '{tournamentId}'.");
        }

        // 3. Pobranie zaakceptowanych drużyn
        var acceptedTeams = await _unitOfWork.Teams.GetByTournamentIdAndStatusAsync(
            tournamentId,
            TeamRegistrationStatus.Accepted,
            cancellationToken);

        if (acceptedTeams.Count < 2)
        {
            throw new InvalidOperationException("Do wygenerowania terminarza wymagane są co najmniej 2 zaakceptowane drużyny.");
        }

        var teamIds = acceptedTeams.Select(t => t.Id).ToList();
        var createdMatches = new List<Match>();

        // 4. Generowanie meczów
        if (tournament.Format == TournamentFormat.League)
        {
            var rounds = RoundRobinScheduler.Generate(teamIds);

            foreach (var round in rounds)
            {
                for (var i = 0; i < round.Pairings.Count; i++)
                {
                    var pairing = round.Pairings[i];
                    var position = i + 1;

                    var match = new Match(
                        tournamentId,
                        round.Number,
                        position,
                        pairing.HomeTeamId,
                        pairing.AwayTeamId);

                    createdMatches.Add(match);
                }
            }
        }
        else // Format pucharowy (Cup)
        {
            var rounds = KnockoutBracketGenerator.Generate(teamIds);

            foreach (var round in rounds)
            {
                foreach (var bracketMatch in round.Matches)
                {
                    var match = new Match(
                        tournamentId,
                        round.Number,
                        bracketMatch.Position,
                        bracketMatch.HomeTeamId,
                        bracketMatch.AwayTeamId);

                    if (bracketMatch.IsBye)
                    {
                        match.MarkAsBye();
                    }

                    createdMatches.Add(match);
                }
            }
        }

        // 5. Zapis encji Match w bazie
        foreach (var match in createdMatches)
        {
            await _unitOfWork.Matches.AddAsync(match, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return createdMatches;
    }
}
