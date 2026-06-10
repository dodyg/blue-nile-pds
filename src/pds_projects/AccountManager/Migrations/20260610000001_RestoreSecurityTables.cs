using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountManager.Migrations
{
    /// <inheritdoc />
    public partial class RestoreSecurityTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "used_refresh_token",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Did = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    UsedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_used_refresh_token", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "device",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    AccountDid = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    SessionId = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "account_device",
                columns: table => new
                {
                    Did = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_device", x => new { x.Did, x.DeviceId });
                });

            migrationBuilder.CreateTable(
                name: "authorization_request",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Did = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Parameters = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authorization_request", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "authorized_client",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Did = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    ClientId = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authorized_client", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "token",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Did = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_token", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "lexicon",
                columns: table => new
                {
                    Nsid = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Uri = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Cid = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Def = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lexicon", x => x.Nsid);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "used_refresh_token");
            migrationBuilder.DropTable(name: "device");
            migrationBuilder.DropTable(name: "account_device");
            migrationBuilder.DropTable(name: "authorization_request");
            migrationBuilder.DropTable(name: "authorized_client");
            migrationBuilder.DropTable(name: "token");
            migrationBuilder.DropTable(name: "lexicon");
        }
    }
}
