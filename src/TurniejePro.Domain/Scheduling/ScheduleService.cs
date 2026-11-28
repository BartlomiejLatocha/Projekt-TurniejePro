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
    public Task<IReadOnlyList<Match>> GenerateScheduleAsync(
        Guid tournamentId,
        ScheduleSeeding seeding,
        CancellationToken cancellationToken = default) =>
        GenerateScheduleAsync(tournamentId, seeding, dates: null, cancellationToken);

    /// <summary>
    /// Jak wyżej, a gdy podano <paramref name="dates"/>, każdy mecz rundy n dostaje datę rundy n (KAN-332).
    /// W formacie grupy + puchar daty dostają rundy fazy grupowej; mecze play-off powstają później i datę ustawia
    /// organizator (<see cref="SetRoundDateAsync"/>). Data 1. rundy nie może być wcześniejsza niż koniec zapisów.
    /// </summary>
    public async Task<IReadOnlyList<Match>> GenerateScheduleAsync(
        Guid tournamentId,
        ScheduleSeeding seeding,
        RoundDates? dates,
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

        if (dates is not null)
        {
            EnsureNotBeforeRegistrationEnds(tournament, dates.FirstRoundDate);
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

        // 5. Daty rund (opcjonalne) i zapis encji Match w bazie. Datę dostają też wolne losy i mecze „do ustalenia”.
        foreach (var match in createdMatches)
        {
            if (dates is not null)
            {
                match.ScheduledAt = dates.For(match.Round);
            }

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

    /// <summary>
    /// Ustawia (lub usuwa, gdy <paramref name="date"/> to null) datę wszystkich meczów rundy (KAN-332), np. żeby
    /// przesunąć kolejkę wypadającą w święta albo nadać datę rundom play-off po fazie grupowej.
    /// Odmawia: runda nie istnieje → 404; turniej zakończony → 400 (admin może); runda ma wpisany wynik → 409;
    /// data przed końcem zapisów → 400. Kolejności dat między rundami nie wymuszamy.
    /// </summary>
    /// <returns>Mecze rundy z nową datą, posortowane według pozycji.</returns>
    public async Task<IReadOnlyList<Match>> SetRoundDateAsync(
        Guid tournamentId,
        int round,
        DateTimeOffset? date,
        bool asAdmin = false,
        CancellationToken cancellationToken = default)
    {
        var tournament = await _unitOfWork.Tournaments.GetByIdAsync(tournamentId, cancellationToken)
            ?? throw new NotFoundException($"Nie odnaleziono turnieju o ID '{tournamentId}'.");

        var matches = (await _unitOfWork.Matches.GetByTournamentIdAsync(tournamentId, cancellationToken))
            .Where(m => m.Round == round)
            .OrderBy(m => m.Position)
            .ToList();

        if (matches.Count == 0)
        {
            throw new NotFoundException($"Terminarz turnieju nie ma rundy {round}.");
        }

        if (tournament.Status == TournamentStatus.Finished && !asAdmin)
        {
            throw new BusinessRuleException("Nie można zmieniać dat w zakończonym turnieju.");
        }

        if (matches.Any(m => !m.IsBye && m.HomeScore is not null))
        {
            throw new ConflictException($"Nie można zmienić daty rundy {round}, bo są w niej już wpisane wyniki.");
        }

        if (date is { } newDate)
        {
            EnsureNotBeforeRegistrationEnds(tournament, newDate);
        }

        foreach (var match in matches)
        {
            match.ScheduledAt = date;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return matches;
    }

    private static void EnsureNotBeforeRegistrationEnds(Tournament tournament, DateTimeOffset roundDate)
    {
        if (roundDate < tournament.StartsAt)
        {
            // Komunikat trafia do użytkownika, więc godzina jest w czasie polskim, tak jak w UI
            var registrationEnds = PolishTime.ToPolish(tournament.StartsAt);
            throw new BusinessRuleException(string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"Data rundy nie może być wcześniejsza niż koniec zapisów ({registrationEnds:dd.MM.yyyy HH:mm})."));
        }
    }
}
