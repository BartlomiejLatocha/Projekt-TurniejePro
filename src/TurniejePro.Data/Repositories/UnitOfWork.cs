using TurniejePro.Domain;
using TurniejePro.Domain.Repositories;

namespace TurniejePro.Data.Repositories;

/// <summary>
/// Implementacja wzorca Unit of Work.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly TurniejeProDbContext _context;

    public ITournamentRepository Tournaments { get; }
    public ITeamRepository Teams { get; }
    public IRepository<Match> Matches { get; }
    public IRepository<CollectibleToken> Tokens { get; }

    public UnitOfWork(TurniejeProDbContext context)
    {
        _context = context;
        Tournaments = new TournamentRepository(context);
        Teams = new TeamRepository(context);
        Matches = new Repository<Match>(context);
        Tokens = new Repository<CollectibleToken>(context);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
