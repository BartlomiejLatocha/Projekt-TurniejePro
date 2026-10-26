using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TurniejePro.Data.Identity;
using TurniejePro.Domain;

namespace TurniejePro.Data;

/// <summary>
/// Główny kontekst bazy danych aplikacji TurniejePro zintegrowany z ASP.NET Core Identity.
/// </summary>
public class TurniejeProDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(TurniejeProDbContext).Assembly);
    }
}
