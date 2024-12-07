using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountManager.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "actor",
                columns: table => new
                {
                    Did = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Handle = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TakedownRef = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    DeactivatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeleteAfter = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_actor", x => x.Did);
                });

            migrationBuilder.CreateTable(
                name: "app_password",
                columns: table => new
                {
                    Did = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    PasswordSCrypt = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Privileged = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_password", x => new { x.Did, x.Name });
                });

            migrationBuilder.CreateTable(
                name: "invite_code",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    AvailableUses = table.Column<int>(type: "INTEGER", nullable: false),
                    Disabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ForAccount = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invite_code", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "invite_code_use",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    UsedBy = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    UsedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invite_code_use", x => new { x.Code, x.UsedBy });
                });

            migrationBuilder.CreateTable(
                name: "refresh_token",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Did = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AppPasswordName = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    NextId = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_token", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "repo_root",
                columns: table => new
                {
                    Did = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Cid = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Rev = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    IndexedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repo_root", x => x.Did);
                });

            migrationBuilder.CreateTable(
                name: "account",
                columns: table => new
                {
                    Did = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    PasswordSCrypt = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    EmailConfirmedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    InvitesDisabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account", x => x.Did);
                    table.ForeignKey(
                        name: "FK_account_actor_Did",
                        column: x => x.Did,
                        principalTable: "actor",
                        principalColumn: "Did");
                });

            migrationBuilder.CreateIndex(
                name: "IX_actor_CreatedAt_Did",
                table: "actor",
                columns: new[] { "CreatedAt", "Did" });

            migrationBuilder.CreateIndex(
                name: "IX_invite_code_ForAccount",
                table: "invite_code",
                column: "ForAccount");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_token_Did",
                table: "refresh_token",
                column: "Did");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account");

            migrationBuilder.DropTable(
                name: "app_password");

            migrationBuilder.DropTable(
                name: "invite_code");

            migrationBuilder.DropTable(
                name: "invite_code_use");

            migrationBuilder.DropTable(
                name: "refresh_token");

            migrationBuilder.DropTable(
                name: "repo_root");

            migrationBuilder.DropTable(
                name: "actor");
        }
    }
}
