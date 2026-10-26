namespace TurniejePro.Domain.Repositories;

/// <summary>
/// Interfejs wzorca Unit of Work do atomowego zarządzania transakcjami i repozytoriami.
/// </summary>
public interface IUnitOfWork
{
    ITournamentRepository Tournaments { get; }
    ITeamRepository Teams { get; }
    IRepository<Match> Matches { get; }
    IRepository<CollectibleToken> Tokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
