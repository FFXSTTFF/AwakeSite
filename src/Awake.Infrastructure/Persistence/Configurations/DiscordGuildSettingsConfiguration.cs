using Awake.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Awake.Infrastructure.Persistence.Configurations;

public class DiscordGuildSettingsConfiguration : IEntityTypeConfiguration<DiscordGuildSettings>
{
    public void Configure(EntityTypeBuilder<DiscordGuildSettings> builder)
    {
        builder.HasKey(x => x.GuildId);
        builder.Property(x => x.GuildId).HasMaxLength(30);
        builder.Property(x => x.AdminChannelId).HasMaxLength(30);
        builder.Property(x => x.AdminRoleId).HasMaxLength(30);
        builder.Property(x => x.TicketCategoryId).HasMaxLength(30);
    }
}
