using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlueNilePds.Pds.PendingAccounts.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingEmailTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PendingRegistrationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Purpose = table.Column<int>(type: "INTEGER", nullable: false),
                    Token = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    NewEmail = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingEmailTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingRegistrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Handle = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    PasswordScrypt = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    InviteCode = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RejectionReason = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    EmailConfirmedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingRegistrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PendingRegistrationId = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    AvatarBlobRef = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Location = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    AccountType = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingProfiles_PendingRegistrations_PendingRegistrationId",
                        column: x => x.PendingRegistrationId,
                        principalTable: "PendingRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingEmailTokens_PendingRegistrationId_Purpose",
                table: "PendingEmailTokens",
                columns: new[] { "PendingRegistrationId", "Purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_PendingProfiles_PendingRegistrationId",
                table: "PendingProfiles",
                column: "PendingRegistrationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingRegistrations_Email",
                table: "PendingRegistrations",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingRegistrations_Handle",
                table: "PendingRegistrations",
                column: "Handle",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingRegistrations_Status",
                table: "PendingRegistrations",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingEmailTokens");

            migrationBuilder.DropTable(
                name: "PendingProfiles");

            migrationBuilder.DropTable(
                name: "PendingRegistrations");
        }
    }
}
