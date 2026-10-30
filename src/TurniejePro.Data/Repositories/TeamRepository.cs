using Microsoft.EntityFrameworkCore;
using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using TurniejePro.Domain.Repositories;

namespace TurniejePro.Data.Repositories;

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
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Team?> GetWithPlayersAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(t => t.Players)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Team>> GetByCaptainIdAsync(Guid captainId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(t => t.Players)
            .Where(t => t.CaptainId == captainId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Team>> GetByStatusAsync(Guid tournamentId, TeamRegistrationStatus status, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(t => t.Players)
            .Where(t => t.TournamentId == tournamentId && t.Status == status)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNameInTournamentAsync(Guid tournamentId, string name, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(t => t.TournamentId == tournamentId && t.Name == name, cancellationToken);
    }
}
