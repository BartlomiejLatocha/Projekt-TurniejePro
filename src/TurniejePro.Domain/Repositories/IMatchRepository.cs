namespace TurniejePro.Domain.Repositories;

public interface IMatchRepository : IRepository<Match>
{
    /// <summary>
    /// Pobiera mecze turnieju wraz z podpiętymi drużynami (HomeTeam, AwayTeam), posortowane według Round i Position.
    /// </summary>
    Task<IReadOnlyList<Match>> GetByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sprawdza, czy dla danego turnieju został już wygenerowany terminarz (istnieją mecze).
    /// </summary>
    Task<bool> HasScheduleAsync(Guid tournamentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Usuwa wszystkie mecze powiązane z danym turniejem.
    /// </summary>
    Task DeleteByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default);
}
