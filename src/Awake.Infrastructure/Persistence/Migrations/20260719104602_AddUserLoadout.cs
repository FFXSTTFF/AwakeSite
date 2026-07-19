using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Awake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserLoadout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Loadout",
                table: "Users",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Loadout",
                table: "Users");
        }
    }
}
