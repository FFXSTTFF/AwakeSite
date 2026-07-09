using Awake.Domain.Entities;
using Awake.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Awake.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Username)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(x => x.Username)
            .IsUnique();

        builder.Property(x => x.PasswordHash)
            .HasMaxLength(255);

        builder.Property(x => x.DiscordUserId)
            .HasMaxLength(30);

        builder.HasIndex(x => x.DiscordUserId)
            .IsUnique();

        builder.Property(x => x.DiscordUsername)
            .HasMaxLength(100);

        builder.Property(x => x.DiscordAvatarUrl)
            .HasMaxLength(300);

        builder.Property(x => x.Email)
            .HasMaxLength(255);

        builder.Property(x => x.GameNickname)
            .HasMaxLength(50);

        builder.Property(x => x.Rank)
            .HasConversion<int>();
    }
}
