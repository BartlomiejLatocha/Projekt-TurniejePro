using TurniejePro.Domain;
using TurniejePro.Domain.Enums;

namespace TurniejePro.Domain.Repositories;

public interface ITeamRepository : IRepository<Team>
{
    Task<IReadOnlyList<Team>> GetByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<Team?> GetWithPlayersAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Team>> GetByCaptainIdAsync(Guid captainId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Team>> GetByStatusAsync(Guid tournamentId, TeamRegistrationStatus status, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameInTournamentAsync(Guid tournamentId, string name, CancellationToken cancellationToken = default);
}
