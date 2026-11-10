using Microsoft.EntityFrameworkCore;
using TurniejePro.Domain;
using TurniejePro.Domain.Repositories;

namespace TurniejePro.Data.Repositories;

public class MatchRepository : Repository<Match>, IMatchRepository
{
    public MatchRepository(TurniejeProDbContext dbContext) : base(dbContext)
    {
    }

    // Bez AsNoTracking: MatchResultService zmienia pobrane mecze i zapisuje je przez SaveChangesAsync,
    // a EF Core zapisuje tylko obiekty śledzone przez kontekst.
    public async Task<IReadOnlyList<Match>> GetByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => m.TournamentId == tournamentId)
            .OrderBy(m => m.Round)
            .ThenBy(m => m.Position)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasScheduleAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AnyAsync(m => m.TournamentId == tournamentId, cancellationToken);
    }

    public async Task DeleteByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var matches = await DbSet
            .Where(m => m.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

        DbSet.RemoveRange(matches);
    }
}
