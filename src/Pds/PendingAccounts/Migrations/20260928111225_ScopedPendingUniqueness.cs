using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlueNilePds.Pds.PendingAccounts.Migrations
{
    /// <inheritdoc />
    public partial class ScopedPendingUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PendingRegistrations_Email",
                table: "PendingRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_PendingRegistrations_Handle",
                table: "PendingRegistrations");

            migrationBuilder.CreateIndex(
                name: "IX_PendingRegistrations_Email",
                table: "PendingRegistrations",
                column: "Email",
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PendingRegistrations_Handle",
                table: "PendingRegistrations",
                column: "Handle",
                unique: true,
                filter: "[Status] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PendingRegistrations_Email",
                table: "PendingRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_PendingRegistrations_Handle",
                table: "PendingRegistrations");

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
        }
    }
}
