using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurniejePro.Domain;

namespace TurniejePro.Data.Configurations;

public class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.HasKey(p => p.Id);

        // Id nadaje domena (Guid.NewGuid). Bez tego EF traktuje nowego zawodnika jak istniejącego i przy zmianie składu wysyła UPDATE zamiast INSERT.
        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.FullName)
            .IsRequired()
            .HasMaxLength(100);
    }
}
