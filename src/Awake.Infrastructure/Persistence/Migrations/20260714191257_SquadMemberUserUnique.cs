using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Awake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SquadMemberUserUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "SquadMembers" sm
                USING "SquadMembers" newer
                WHERE sm."UserId" = newer."UserId"
                  AND sm."JoinedAt" < newer."JoinedAt";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SquadMembers_UserId",
                table: "SquadMembers",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SquadMembers_UserId",
                table: "SquadMembers");
        }
    }
}
