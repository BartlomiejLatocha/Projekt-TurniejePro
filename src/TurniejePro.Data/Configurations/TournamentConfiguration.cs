using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurniejePro.Domain;
using TurniejePro.Domain.Enums;

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

        builder.Property(t => t.Status)
            .IsRequired()
            .HasDefaultValue(TournamentStatus.Draft);

        builder.Ignore(t => t.IsStarted);

        builder.OwnsOne(t => t.ScoringRules, sr =>
        {
            sr.Property(s => s.PointsForWin)
                .IsRequired()
                .HasDefaultValue(3)
                .ValueGeneratedNever();

            sr.Property(s => s.PointsForDraw)
                .IsRequired()
                .HasDefaultValue(1)
                .ValueGeneratedNever();

            sr.Property(s => s.PointsForLoss)
                .IsRequired()
                .HasDefaultValue(0)
                .ValueGeneratedNever();

            sr.Property(s => s.AllowDraws)
                .IsRequired()
                .HasDefaultValue(true)
                .ValueGeneratedNever();
        });
        builder.Navigation(t => t.ScoringRules).IsRequired();

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
