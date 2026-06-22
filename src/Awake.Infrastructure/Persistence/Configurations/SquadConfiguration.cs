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

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        builder.HasData(
            new { Id = new Guid("11111111-0000-0000-0000-000000000001"), Name = "Отряд 1", Number = 1, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = new Guid("11111111-0000-0000-0000-000000000002"), Name = "Отряд 2", Number = 2, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = new Guid("11111111-0000-0000-0000-000000000003"), Name = "Отряд 3", Number = 3, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = new Guid("11111111-0000-0000-0000-000000000004"), Name = "Отряд 4", Number = 4, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = new Guid("11111111-0000-0000-0000-000000000005"), Name = "Отряд 5", Number = 5, CreatedAt = seedDate, UpdatedAt = seedDate }
        );
    }
}
