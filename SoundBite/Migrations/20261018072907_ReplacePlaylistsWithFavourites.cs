using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoundBite.Migrations
{
    /// <inheritdoc />
    public partial class ReplacePlaylistsWithFavourites : Migration
    {
        /// <inheritdoc />
        // Replaces playlists with per-user favourite tracks.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Likes_Playlists_PlaylistId",
                table: "Likes");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Playlists_PlaylistId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_PlaylistId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Likes_PlaylistId_UserId",
                table: "Likes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Likes_ExactlyOneTarget",
                table: "Likes");

            migrationBuilder.Sql("""
                DELETE FROM "Notifications" WHERE "Type" = 5;
                DELETE FROM "NotificationOptOuts" WHERE "Type" = 5;
                DELETE FROM "Likes" WHERE "PlaylistId" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "PlaylistId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "PlaylistId",
                table: "Likes");

            migrationBuilder.CreateTable(
                name: "FavouriteTracks",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    TrackId = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FavouriteTracks", x => new { x.UserId, x.TrackId });
                    table.ForeignKey(
                        name: "FK_FavouriteTracks_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FavouriteTracks_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT OR IGNORE INTO "FavouriteTracks" ("UserId", "TrackId", "AddedAt")
                SELECT playlist."OwnerId", item."TrackId", MIN(item."AddedAt")
                FROM "PlaylistItems" AS item
                INNER JOIN "Playlists" AS playlist ON playlist."Id" = item."PlaylistId"
                GROUP BY playlist."OwnerId", item."TrackId";
                """);

            migrationBuilder.DropTable(
                name: "PlaylistItems");

            migrationBuilder.DropTable(
                name: "Playlists");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Likes_ExactlyOneTarget",
                table: "Likes",
                sql: "(CASE WHEN \"ReviewId\" IS NOT NULL THEN 1 ELSE 0 END) +\n(CASE WHEN \"TrackId\" IS NOT NULL THEN 1 ELSE 0 END) +\n(CASE WHEN \"CommentId\" IS NOT NULL THEN 1 ELSE 0 END) = 1");

            migrationBuilder.CreateIndex(
                name: "IX_FavouriteTracks_TrackId",
                table: "FavouriteTracks",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_FavouriteTracks_UserId_AddedAt_TrackId",
                table: "FavouriteTracks",
                columns: new[] { "UserId", "AddedAt", "TrackId" });
        }

        /// <inheritdoc />
        // Restores playlists and removes favourite tracks.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Likes_ExactlyOneTarget",
                table: "Likes");

            migrationBuilder.AddColumn<int>(
                name: "PlaylistId",
                table: "Notifications",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlaylistId",
                table: "Likes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Playlists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    IsPublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Playlists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Playlists_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaylistItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlaylistId = table.Column<int>(type: "INTEGER", nullable: false),
                    TrackId = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaylistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaylistItems_Playlists_PlaylistId",
                        column: x => x.PlaylistId,
                        principalTable: "Playlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlaylistItems_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO "Playlists" ("Name", "Description", "OwnerId", "IsPublic", "CreatedAt", "UpdatedAt")
                SELECT 'Favourites', '', "UserId", 1, MIN("AddedAt"), MAX("AddedAt")
                FROM "FavouriteTracks"
                GROUP BY "UserId";

                INSERT INTO "PlaylistItems" ("PlaylistId", "TrackId", "Position", "AddedAt")
                SELECT playlist."Id", favourite."TrackId",
                    ROW_NUMBER() OVER (PARTITION BY favourite."UserId" ORDER BY favourite."AddedAt", favourite."TrackId") - 1,
                    favourite."AddedAt"
                FROM "FavouriteTracks" AS favourite
                INNER JOIN "Playlists" AS playlist ON playlist."OwnerId" = favourite."UserId";
                """);

            migrationBuilder.DropTable(
                name: "FavouriteTracks");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_PlaylistId",
                table: "Notifications",
                column: "PlaylistId");

            migrationBuilder.CreateIndex(
                name: "IX_Likes_PlaylistId_UserId",
                table: "Likes",
                columns: new[] { "PlaylistId", "UserId" },
                unique: true,
                filter: "\"PlaylistId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Likes_ExactlyOneTarget",
                table: "Likes",
                sql: "(CASE WHEN \"ReviewId\" IS NOT NULL THEN 1 ELSE 0 END) +\n(CASE WHEN \"TrackId\" IS NOT NULL THEN 1 ELSE 0 END) +\n(CASE WHEN \"PlaylistId\" IS NOT NULL THEN 1 ELSE 0 END) +\n(CASE WHEN \"CommentId\" IS NOT NULL THEN 1 ELSE 0 END) = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistItems_PlaylistId_Position",
                table: "PlaylistItems",
                columns: new[] { "PlaylistId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistItems_PlaylistId_TrackId",
                table: "PlaylistItems",
                columns: new[] { "PlaylistId", "TrackId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistItems_TrackId",
                table: "PlaylistItems",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_Playlists_OwnerId",
                table: "Playlists",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Likes_Playlists_PlaylistId",
                table: "Likes",
                column: "PlaylistId",
                principalTable: "Playlists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Playlists_PlaylistId",
                table: "Notifications",
                column: "PlaylistId",
                principalTable: "Playlists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
