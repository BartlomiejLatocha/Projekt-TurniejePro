using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurniejePro.Domain;
using TurniejePro.Domain.Enums;

namespace TurniejePro.Data.Configurations;

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.ContactEmail)
            .HasMaxLength(100);

        builder.Property(t => t.IsAccepted)
            .IsRequired();

        builder.Property(t => t.Status)
            .IsRequired()
            .HasDefaultValue(TeamRegistrationStatus.Pending)
            .HasSentinel(0);

        builder.Property(t => t.RegisteredAt)
            .IsRequired();

        builder.Property(t => t.LogoContentType)
            .HasMaxLength(50);

        builder.Property(t => t.Logo)
            .HasMaxLength(Team.MaxLogoBytes);

        builder.HasMany(t => t.Players)
            .WithOne(p => p.Team)
            .HasForeignKey(p => p.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
