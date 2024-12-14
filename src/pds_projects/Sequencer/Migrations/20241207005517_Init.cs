using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sequencer.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RepoSeqs",
                columns: table => new
                {
                    Seq = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Did = table.Column<string>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", nullable: false),
                    Event = table.Column<byte[]>(type: "BLOB", nullable: false),
                    Invalidated = table.Column<bool>(type: "INTEGER", nullable: false),
                    SequencedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepoSeqs", x => x.Seq);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepoSeqs");
        }
    }
}
