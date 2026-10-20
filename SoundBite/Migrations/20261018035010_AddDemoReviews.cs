using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoundBite.Migrations
{
    /// <inheritdoc />
    public partial class AddDemoReviews : Migration
    {
        /// <inheritdoc />
        // Adds demo reviews for the catalogue.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DemoReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeedKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ReviewerLabel = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Rating = table.Column<int>(type: "INTEGER", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    TrackId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemoReviews", x => x.Id);
                    table.CheckConstraint("CK_DemoReviews_Rating", "\"Rating\" >= 1 AND \"Rating\" <= 5");
                    table.ForeignKey(
                        name: "FK_DemoReviews_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DemoReviews_SeedKey",
                table: "DemoReviews",
                column: "SeedKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DemoReviews_TrackId",
                table: "DemoReviews",
                column: "TrackId");
        }

        /// <inheritdoc />
        // Removes the seeded demo reviews.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemoReviews");
        }
    }
}
