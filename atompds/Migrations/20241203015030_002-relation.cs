using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace atompds.Migrations
{
    /// <inheritdoc />
    public partial class _002relation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_actor_account_Did",
                table: "account",
                column: "Did",
                principalTable: "actor",
                principalColumn: "Did");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_actor_account_Did",
                table: "account");
        }
    }
}
