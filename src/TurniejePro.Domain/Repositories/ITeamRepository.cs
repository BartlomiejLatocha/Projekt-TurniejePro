namespace TurniejePro.Domain.Repositories;

/// <summary>
/// Interfejs repozytorium dedykowany dla operacji na drużynach.
/// </summary>
public interface ITeamRepository : IRepository<Team>
{
    /// <summary>
    /// Pobiera listę drużyn zarejestrowanych w danym turnieju wraz ze składami.
    /// </summary>
    Task<IReadOnlyList<Team>> GetByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default);
}
