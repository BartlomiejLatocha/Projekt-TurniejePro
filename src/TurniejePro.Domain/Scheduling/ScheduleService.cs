using TurniejePro.Domain.Enums;
using TurniejePro.Domain.Exceptions;
using TurniejePro.Domain.Repositories;

namespace TurniejePro.Domain.Scheduling;

public class ScheduleService
{
    private readonly IUnitOfWork _unitOfWork;

    public ScheduleService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Generuje terminarz z domyślnym rozstawieniem: stałą kolejnością zgłoszeń drużyn.
    /// </summary>
    public Task<IReadOnlyList<Match>> GenerateScheduleAsync(Guid tournamentId, CancellationToken cancellationToken = default) =>
        GenerateScheduleAsync(tournamentId, ScheduleSeeding.Registration, cancellationToken);

    /// <summary>
    /// Generuje terminarz, rozstawiając zaakceptowane drużyny według <paramref name="seeding"/>: w kolejności zgłoszeń,
    /// losowo albo ręcznie. W pucharze kolejność rozstawienia wyznacza pary pierwszej rundy i wolne losy, a w lidze
    /// układ kolejek.
    /// </summary>
    public async Task<IReadOnlyList<Match>> GenerateScheduleAsync(
        Guid tournamentId,
        ScheduleSeeding seeding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seeding);

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
            throw new NotFoundException($"Nie odnaleziono turnieju o ID '{tournamentId}'.");
        }

        // 3. Pobranie zaakceptowanych drużyn
        var acceptedTeams = await _unitOfWork.Teams.GetByTournamentIdAndStatusAsync(
            tournamentId,
            TeamRegistrationStatus.Accepted,
            cancellationToken);

        if (acceptedTeams.Count < 2)
        {
            throw new BusinessRuleException("Do wygenerowania terminarza wymagane są co najmniej 2 zaakceptowane drużyny.");
        }

        var teamIds = TeamSeeder.Seed(acceptedTeams, seeding.Mode, seeding.TeamOrder, seeding.RandomIndex);
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
        else if (tournament.Format == TournamentFormat.GroupsAndCup)
        {
            var groupCount = GroupStage.GroupCountFor(acceptedTeams.Count);
            if (groupCount == 0)
            {
                throw new BusinessRuleException(
                    $"Faza grupowa wymaga co najmniej {GroupStage.MinTeamsPerGroup * 2} zaakceptowanych drużyn (po co najmniej {GroupStage.MinTeamsPerGroup} w grupie).");
            }

            var teamsById = acceptedTeams.ToDictionary(t => t.Id);
            var groups = GroupStage.Distribute(teamIds, groupCount);

            // Mecze każdy z każdym w grupie. Pozycje są unikalne w rundzie w obrębie całego turnieju.
            var positionInRound = new Dictionary<int, int>();
            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                foreach (var teamId in groups[groupIndex])
                {
                    teamsById[teamId].GroupNumber = groupIndex + 1;
                }

                foreach (var round in RoundRobinScheduler.Generate(groups[groupIndex]))
                {
                    foreach (var pairing in round.Pairings)
                    {
                        positionInRound[round.Number] = positionInRound.GetValueOrDefault(round.Number) + 1;

                        var match = new Match(
                            tournamentId,
                            round.Number,
                            positionInRound[round.Number],
                            pairing.HomeTeamId,
                            pairing.AwayTeamId)
                        {
                            IsGroupStage = true
                        };

                        createdMatches.Add(match);
                    }
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

        // 6. Terminarz rozpoczyna turniej: zapisy się zamykają, a format i punktacja zostają zablokowane (KAN-290).
        // Turniej rozpoczęty wcześniej ręcznie (bez terminarza) zostaje w trakcie.
        if (!tournament.IsStarted)
        {
            tournament.Start();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return createdMatches;
    }

    /// <summary>
    /// Usuwa terminarz, dopóki nie ma w nim żadnego wyniku, i cofa turniej do zapisów, żeby organizator mógł zmienić
    /// skład drużyn i wygenerować terminarz od nowa. Wpisane wyniki chronią terminarz przed usunięciem (409).
    /// </summary>
    public async Task DeleteScheduleAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var tournament = await _unitOfWork.Tournaments.GetByIdAsync(tournamentId, cancellationToken)
            ?? throw new NotFoundException($"Nie odnaleziono turnieju o ID '{tournamentId}'.");

        var matches = await _unitOfWork.Matches.GetByTournamentIdAsync(tournamentId, cancellationToken);
        if (matches.Count == 0)
        {
            throw new NotFoundException($"Turniej o ID '{tournamentId}' nie posiada wygenerowanego terminarza do usunięcia.");
        }

        if (matches.Any(m => !m.IsBye && m.HomeScore is not null))
        {
            throw new ConflictException("Nie można usunąć terminarza, w którym są już wpisane wyniki.");
        }

        await _unitOfWork.Matches.DeleteByTournamentIdAsync(tournamentId, cancellationToken);
        tournament.ReturnToRegistration();

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
