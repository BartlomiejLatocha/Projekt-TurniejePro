using Microsoft.EntityFrameworkCore;
using TurniejePro.Domain;
using TurniejePro.Domain.Repositories;

namespace TurniejePro.Data.Repositories;

/// <summary>
/// Implementacja repozytorium drużyn.
/// </summary>
public class TeamRepository : Repository<Team>, ITeamRepository
{
    public TeamRepository(TurniejeProDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Team>> GetByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(t => t.Players)
            .Where(t => t.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);
    }
}
