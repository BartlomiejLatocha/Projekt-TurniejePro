using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurniejePro.Domain;

namespace TurniejePro.Data.Configurations;

public class TournamentConfiguration : IEntityTypeConfiguration<Tournament>
{
    public void Configure(EntityTypeBuilder<Tournament> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.Discipline)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.Format)
            .IsRequired();

        builder.Property(t => t.StartsAt)
            .IsRequired();

        builder.Property(t => t.MaxTeams)
            .IsRequired();

        builder.Property(t => t.IsStarted)
            .IsRequired();

        builder.HasMany(t => t.Teams)
            .WithOne(tm => tm.Tournament)
            .HasForeignKey(tm => tm.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Matches)
            .WithOne(m => m.Tournament)
            .HasForeignKey(m => m.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Tokens)
            .WithOne(tk => tk.Tournament)
            .HasForeignKey(tk => tk.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
