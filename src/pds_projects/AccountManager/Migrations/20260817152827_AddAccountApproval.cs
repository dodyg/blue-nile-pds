using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountManager.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedAt",
                table: "actor",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountType",
                table: "account",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "account",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SuspendedAt",
                table: "actor");

            migrationBuilder.DropColumn(
                name: "AccountType",
                table: "account");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "account");
        }
    }
}
