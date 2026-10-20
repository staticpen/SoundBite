using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoundBite.Migrations
{
    /// <inheritdoc />
    public partial class ThreadedReviewComments : Migration
    {
        /// <inheritdoc />
        // Adds parent links so review comments can have replies.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Comments_ReviewId_CreatedAt",
                table: "Comments");

            migrationBuilder.AddColumn<int>(
                name: "ParentCommentId",
                table: "Comments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ParentCommentId",
                table: "Comments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ReviewId_ParentCommentId_CreatedAt",
                table: "Comments",
                columns: new[] { "ReviewId", "ParentCommentId", "CreatedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Comments_NoSelfReply",
                table: "Comments",
                sql: "\"ParentCommentId\" IS NULL OR \"ParentCommentId\" <> \"Id\"");

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Comments_ParentCommentId",
                table: "Comments",
                column: "ParentCommentId",
                principalTable: "Comments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        // Removes parent links from review comments.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Comments_ParentCommentId",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_ParentCommentId",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_ReviewId_ParentCommentId_CreatedAt",
                table: "Comments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Comments_NoSelfReply",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "ParentCommentId",
                table: "Comments");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ReviewId_CreatedAt",
                table: "Comments",
                columns: new[] { "ReviewId", "CreatedAt" });
        }
    }
}
