using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace atompds.Migrations.ActorStoreDbMigrations
{
    /// <inheritdoc />
    public partial class _001init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_pref",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ValueJson = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_pref", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "backlink",
                columns: table => new
                {
                    Uri = table.Column<string>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    LinkTo = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backlink", x => new { x.Uri, x.Path });
                });

            migrationBuilder.CreateTable(
                name: "blob",
                columns: table => new
                {
                    Cid = table.Column<string>(type: "TEXT", nullable: false),
                    MimeType = table.Column<string>(type: "TEXT", nullable: false),
                    Size = table.Column<int>(type: "INTEGER", nullable: false),
                    TempKey = table.Column<string>(type: "TEXT", nullable: true),
                    Width = table.Column<int>(type: "INTEGER", nullable: true),
                    Height = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TakedownRef = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blob", x => x.Cid);
                });

            migrationBuilder.CreateTable(
                name: "record",
                columns: table => new
                {
                    Uri = table.Column<string>(type: "TEXT", nullable: false),
                    Cid = table.Column<string>(type: "TEXT", nullable: false),
                    Collection = table.Column<string>(type: "TEXT", nullable: false),
                    Rkey = table.Column<string>(type: "TEXT", nullable: false),
                    RepoRev = table.Column<string>(type: "TEXT", nullable: false),
                    IndexedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TakedownRef = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_record", x => x.Uri);
                });

            migrationBuilder.CreateTable(
                name: "record_blob",
                columns: table => new
                {
                    BlobCid = table.Column<string>(type: "TEXT", nullable: false),
                    RecordUri = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_record_blob", x => new { x.BlobCid, x.RecordUri });
                });

            migrationBuilder.CreateTable(
                name: "repo_block",
                columns: table => new
                {
                    Cid = table.Column<string>(type: "TEXT", nullable: false),
                    RepoRev = table.Column<string>(type: "TEXT", nullable: false),
                    Size = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repo_block", x => x.Cid);
                });

            migrationBuilder.CreateTable(
                name: "repo_root",
                columns: table => new
                {
                    Did = table.Column<string>(type: "TEXT", nullable: false),
                    Cid = table.Column<string>(type: "TEXT", nullable: false),
                    Rev = table.Column<string>(type: "TEXT", nullable: false),
                    IndexedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repo_root", x => x.Did);
                });

            migrationBuilder.CreateIndex(
                name: "IX_backlink_Path_LinkTo",
                table: "backlink",
                columns: new[] { "Path", "LinkTo" });

            migrationBuilder.CreateIndex(
                name: "IX_blob_TempKey",
                table: "blob",
                column: "TempKey");

            migrationBuilder.CreateIndex(
                name: "IX_record_Cid",
                table: "record",
                column: "Cid");

            migrationBuilder.CreateIndex(
                name: "IX_record_Collection",
                table: "record",
                column: "Collection");

            migrationBuilder.CreateIndex(
                name: "IX_record_RepoRev",
                table: "record",
                column: "RepoRev");

            migrationBuilder.CreateIndex(
                name: "IX_repo_block_RepoRev_Cid",
                table: "repo_block",
                columns: new[] { "RepoRev", "Cid" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_pref");

            migrationBuilder.DropTable(
                name: "backlink");

            migrationBuilder.DropTable(
                name: "blob");

            migrationBuilder.DropTable(
                name: "record");

            migrationBuilder.DropTable(
                name: "record_blob");

            migrationBuilder.DropTable(
                name: "repo_block");

            migrationBuilder.DropTable(
                name: "repo_root");
        }
    }
}
