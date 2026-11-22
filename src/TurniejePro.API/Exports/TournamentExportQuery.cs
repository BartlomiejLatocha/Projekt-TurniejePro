using TurniejePro.Api.Mapping;
using TurniejePro.Contracts.Tournaments;
using TurniejePro.Domain.Exceptions;
using TurniejePro.Domain.Repositories;
using DomainFormat = TurniejePro.Domain.Enums.TournamentFormat;
using TeamRegistrationStatus = TurniejePro.Domain.Enums.TeamRegistrationStatus;

namespace TurniejePro.Api.Exports;

/// <summary>Pobiera terminarz i tabelę turnieju do wygenerowania raportu.</summary>
public sealed class TournamentExportQuery(IUnitOfWork unitOfWork)
{
    public async Task<TournamentExportData> GetAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var tournament = await unitOfWork.Tournaments.GetByIdAsync(tournamentId, cancellationToken)
            ?? throw new NotFoundException($"Nie odnaleziono turnieju o ID '{tournamentId}'.");

        var matches = await unitOfWork.Matches.GetByTournamentIdAsync(tournamentId, cancellationToken);
        var schedule = matches.ToScheduleResponse(tournamentId, (TournamentFormat)tournament.Format);
        if (tournament.Format == DomainFormat.Cup)
        {
            return new TournamentExportData(tournament.Name, schedule, null);
        }

        var teams = await unitOfWork.Teams.GetByStatusAsync(
            tournamentId, TeamRegistrationStatus.Accepted, cancellationToken);
        return new TournamentExportData(tournament.Name, schedule, tournament.ToStandingsResponse(teams, matches));
    }
}
