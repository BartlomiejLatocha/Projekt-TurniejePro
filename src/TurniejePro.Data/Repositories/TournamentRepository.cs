using Microsoft.EntityFrameworkCore;
using TurniejePro.Domain;
using TurniejePro.Domain.Repositories;

namespace TurniejePro.Data.Repositories;

public class TournamentRepository : Repository<Tournament>, ITournamentRepository
{
    public TournamentRepository(TurniejeProDbContext context) : base(context)
    {
    }

    public async Task<Tournament?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(t => t.Teams)
                .ThenInclude(tm => tm.Players)
            .Include(t => t.Matches)
            .Include(t => t.Tokens)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Tournament>> GetByOrganizerIdAsync(Guid organizerId, CancellationToken cancellationToken = default)
    {
        // Drużyny są potrzebne do liczników zgłoszeń w widoku „Moje turnieje”
        return await DbSet
            .AsNoTracking()
            .Include(t => t.Teams)
            .Where(t => t.OrganizerId == organizerId)
            .OrderBy(t => t.Discipline)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Tournament> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search = null,
        IReadOnlyCollection<string>? disciplineCodes = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            // SQLite (LIKE, lower) nie rozróżnia wielkości liter tylko dla liter angielskich, więc „żubry” nie znalazłoby
            // „Żubry”. Dlatego pobieramy same identyfikatory, nazwy i dyscypliny i porównujemy je w aplikacji
            // (SearchText). Przy skali projektu to kilkaset krótkich wierszy.
            var codes = disciplineCodes ?? [];
            var candidates = await DbSet
                .AsNoTracking()
                .Select(t => new { t.Id, t.Name, t.Discipline })
                .ToListAsync(cancellationToken);

            var matchingIds = candidates
                .Where(t => SearchText.Contains(t.Name, search)
                    || SearchText.Contains(t.Discipline, search)
                    || codes.Contains(t.Discipline, StringComparer.OrdinalIgnoreCase))
                .Select(t => t.Id)
                .ToList();

            query = query.Where(t => matchingIds.Contains(t.Id));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(t => t.Discipline)
            .ThenBy(t => t.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(t => t.Id == id, cancellationToken);
    }
}
