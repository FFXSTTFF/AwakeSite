using Awake.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Awake.Infrastructure.Persistence.Configurations;

public class SquadConfiguration : IEntityTypeConfiguration<Squad>
{
    public void Configure(EntityTypeBuilder<Squad> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(x => x.Number)
            .IsUnique();

        builder.ToTable(t => t.HasCheckConstraint("CK_Squad_Number", "\"Number\" BETWEEN 1 AND 5"));
    }
}
