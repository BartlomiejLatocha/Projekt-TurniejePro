namespace TurniejePro.Domain.Repositories;

public interface ITournamentRepository : IRepository<Tournament>
{
    Task<Tournament?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tournament>> GetByOrganizerIdAsync(Guid organizerId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Tournament> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
