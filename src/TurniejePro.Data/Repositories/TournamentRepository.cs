using Microsoft.EntityFrameworkCore;
using TurniejePro.Domain;
using TurniejePro.Domain.Repositories;

namespace TurniejePro.Data.Repositories;

/// <summary>
/// Implementacja repozytorium turniejów.
/// </summary>
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
}
