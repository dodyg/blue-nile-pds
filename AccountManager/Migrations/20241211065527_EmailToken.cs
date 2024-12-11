using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountManager.Migrations
{
    /// <inheritdoc />
    public partial class EmailToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "email_token",
                columns: table => new
                {
                    Purpose = table.Column<int>(type: "INTEGER", nullable: false),
                    Did = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Token = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_token", x => new { x.Purpose, x.Did });
                });

            migrationBuilder.CreateIndex(
                name: "IX_email_token_Purpose_Token",
                table: "email_token",
                columns: new[] { "Purpose", "Token" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_token");
        }
    }
}
