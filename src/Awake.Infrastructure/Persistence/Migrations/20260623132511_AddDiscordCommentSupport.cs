using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Awake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscordCommentSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "AuthorId",
                table: "TicketComments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "DiscordAuthorName",
                table: "TicketComments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscordAuthorName",
                table: "TicketComments");

            migrationBuilder.AlterColumn<Guid>(
                name: "AuthorId",
                table: "TicketComments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
