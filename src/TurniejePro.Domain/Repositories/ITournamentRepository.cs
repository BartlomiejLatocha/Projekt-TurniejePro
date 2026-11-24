namespace TurniejePro.Domain.Repositories;

public interface ITournamentRepository : IRepository<Tournament>
{
    Task<Tournament?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tournament>> GetByOrganizerIdAsync(Guid organizerId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Strona turniejów. <paramref name="search"/> szuka w nazwie i w kodzie dyscypliny (bez wielkości liter i polskich
    /// znaków), a <paramref name="disciplineCodes"/> dokłada turnieje z tych dyscyplin, np. kody pasujące do
    /// wyświetlanej nazwy „Siatkówka”.
    /// </summary>
    Task<(IReadOnlyList<Tournament> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search = null,
        IReadOnlyCollection<string>? disciplineCodes = null,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
