using Awake.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Awake.Infrastructure.Persistence.Configurations;

public class SquadMemberConfiguration : IEntityTypeConfiguration<SquadMember>
{
    public void Configure(EntityTypeBuilder<SquadMember> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Squad)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.SquadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany(x => x.SquadMemberships)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Business rule: only one leader per squad — via filtered unique index
        builder.HasIndex(x => new { x.SquadId, x.IsLeader })
            .HasFilter("\"IsLeader\" = true")
            .IsUnique();

        // Business rule: one user — one squad
        builder.HasIndex(x => x.UserId).IsUnique();
    }
}
