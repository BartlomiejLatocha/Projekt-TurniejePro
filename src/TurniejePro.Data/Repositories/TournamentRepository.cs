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
        return await DbSet
            .Where(t => t.OrganizerId == organizerId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Tournament> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(t => EF.Functions.Like(t.Name, pattern) || EF.Functions.Like(t.Discipline, pattern));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(t => t.Name)
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
