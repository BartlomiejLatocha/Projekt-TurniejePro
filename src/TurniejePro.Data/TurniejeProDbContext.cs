using Microsoft.EntityFrameworkCore;
using TurniejePro.Domain;

namespace TurniejePro.Data;

/// <summary>
/// Główny kontekst bazy danych aplikacji TurniejePro.
/// </summary>
public class TurniejeProDbContext : DbContext
{
    public TurniejeProDbContext(DbContextOptions<TurniejeProDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<CollectibleToken> CollectibleTokens => Set<CollectibleToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TurniejeProDbContext).Assembly);
    }
}
