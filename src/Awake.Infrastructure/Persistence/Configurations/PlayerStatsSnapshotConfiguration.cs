using System.Text.Json;
using Awake.Domain.Entities;
using Awake.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Awake.Infrastructure.Persistence.Configurations;

public class PlayerStatsSnapshotConfiguration : IEntityTypeConfiguration<PlayerStatsSnapshot>
{
    public void Configure(EntityTypeBuilder<PlayerStatsSnapshot> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.GameNickname)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(x => x.GameNickname)
            .IsUnique();

        builder.Property(x => x.Accuracy).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Playtime).IsRequired().HasMaxLength(50);

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        builder.Property(x => x.ClanHistory)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, jsonOptions),
                v => JsonSerializer.Deserialize<List<ClanEntry>>(v, jsonOptions) ?? new List<ClanEntry>());
    }
}
