namespace TurniejePro.Domain.Repositories;

/// <summary>
/// Interfejs repozytorium dedykowany dla operacji na turniejach.
/// </summary>
public interface ITournamentRepository : IRepository<Tournament>
{
    /// <summary>
    /// Pobiera turniej wraz ze wszystkimi powiązanymi drużynami, meczami i tokenami.
    /// </summary>
    Task<Tournament?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
}
