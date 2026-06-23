using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Awake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscordGuildSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscordChannelId",
                table: "Tickets",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DiscordGuildSettings",
                columns: table => new
                {
                    GuildId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AdminChannelId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AdminRoleId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    TicketCategoryId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscordGuildSettings", x => x.GuildId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscordGuildSettings");

            migrationBuilder.DropColumn(
                name: "DiscordChannelId",
                table: "Tickets");
        }
    }
}
