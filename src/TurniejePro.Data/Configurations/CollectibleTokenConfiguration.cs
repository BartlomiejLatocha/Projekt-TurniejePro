using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurniejePro.Domain;

namespace TurniejePro.Data.Configurations;

public class CollectibleTokenConfiguration : IEntityTypeConfiguration<CollectibleToken>
{
    public void Configure(EntityTypeBuilder<CollectibleToken> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(t => new { t.TournamentId, t.Code })
            .IsUnique();

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.Description)
            .HasMaxLength(500);

        builder.Property(t => t.CreatedAt)
            .IsRequired();
    }
}
