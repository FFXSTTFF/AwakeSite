using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Awake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerBoostRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerBoostRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoostType = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerBoostRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerBoostRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerBoostRequests_UserId_BoostType",
                table: "PlayerBoostRequests",
                columns: new[] { "UserId", "BoostType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerBoostRequests");
        }
    }
}
