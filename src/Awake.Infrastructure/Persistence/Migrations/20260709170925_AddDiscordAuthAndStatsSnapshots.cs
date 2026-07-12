using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Awake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscordAuthAndStatsSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AddColumn<string>(
                name: "DiscordAvatarUrl",
                table: "Users",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordUserId",
                table: "Users",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordUsername",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlayerStatsSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameNickname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kills = table.Column<int>(type: "integer", nullable: false),
                    Deaths = table.Column<int>(type: "integer", nullable: false),
                    KdRatio = table.Column<double>(type: "double precision", nullable: false),
                    Accuracy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Playtime = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ClanHistory = table.Column<string>(type: "jsonb", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerStatsSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_DiscordUserId",
                table: "Users",
                column: "DiscordUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerStatsSnapshots_GameNickname",
                table: "PlayerStatsSnapshots",
                column: "GameNickname",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerStatsSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_Users_DiscordUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DiscordAvatarUrl",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DiscordUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DiscordUsername",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);
        }
    }
}
