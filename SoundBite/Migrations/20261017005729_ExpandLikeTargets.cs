using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoundBite.Migrations
{
    /// <inheritdoc />
    public partial class ExpandLikeTargets : Migration
    {
        /// <inheritdoc />
        // Allows likes to target reviews and comments.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Likes_ReviewId_UserId",
                table: "Likes");

            migrationBuilder.AlterColumn<int>(
                name: "ReviewId",
                table: "Likes",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<int>(
                name: "CommentId",
                table: "Likes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlaylistId",
                table: "Likes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrackId",
                table: "Likes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Likes_CommentId_UserId",
                table: "Likes",
                columns: new[] { "CommentId", "UserId" },
                unique: true,
                filter: "\"CommentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Likes_PlaylistId_UserId",
                table: "Likes",
                columns: new[] { "PlaylistId", "UserId" },
                unique: true,
                filter: "\"PlaylistId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Likes_ReviewId_UserId",
                table: "Likes",
                columns: new[] { "ReviewId", "UserId" },
                unique: true,
                filter: "\"ReviewId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Likes_TrackId_UserId",
                table: "Likes",
                columns: new[] { "TrackId", "UserId" },
                unique: true,
                filter: "\"TrackId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Likes_ExactlyOneTarget",
                table: "Likes",
                sql: "(CASE WHEN \"ReviewId\" IS NOT NULL THEN 1 ELSE 0 END) +\n(CASE WHEN \"TrackId\" IS NOT NULL THEN 1 ELSE 0 END) +\n(CASE WHEN \"PlaylistId\" IS NOT NULL THEN 1 ELSE 0 END) +\n(CASE WHEN \"CommentId\" IS NOT NULL THEN 1 ELSE 0 END) = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Likes_Comments_CommentId",
                table: "Likes",
                column: "CommentId",
                principalTable: "Comments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Likes_Playlists_PlaylistId",
                table: "Likes",
                column: "PlaylistId",
                principalTable: "Playlists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Likes_Tracks_TrackId",
                table: "Likes",
                column: "TrackId",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        // Restricts likes to reviews again.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Likes_Comments_CommentId",
                table: "Likes");

            migrationBuilder.DropForeignKey(
                name: "FK_Likes_Playlists_PlaylistId",
                table: "Likes");

            migrationBuilder.DropForeignKey(
                name: "FK_Likes_Tracks_TrackId",
                table: "Likes");

            migrationBuilder.DropIndex(
                name: "IX_Likes_CommentId_UserId",
                table: "Likes");

            migrationBuilder.DropIndex(
                name: "IX_Likes_PlaylistId_UserId",
                table: "Likes");

            migrationBuilder.DropIndex(
                name: "IX_Likes_ReviewId_UserId",
                table: "Likes");

            migrationBuilder.DropIndex(
                name: "IX_Likes_TrackId_UserId",
                table: "Likes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Likes_ExactlyOneTarget",
                table: "Likes");

            migrationBuilder.DropColumn(
                name: "CommentId",
                table: "Likes");

            migrationBuilder.DropColumn(
                name: "PlaylistId",
                table: "Likes");

            migrationBuilder.DropColumn(
                name: "TrackId",
                table: "Likes");

            migrationBuilder.AlterColumn<int>(
                name: "ReviewId",
                table: "Likes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Likes_ReviewId_UserId",
                table: "Likes",
                columns: new[] { "ReviewId", "UserId" },
                unique: true);
        }
    }
}
