using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoundBite.Migrations
{
    /// <inheritdoc />
    public partial class ConstrainReviewRatings : Migration
    {
        /// <inheritdoc />
        // Restricts review ratings to the supported range.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Reviews_Rating",
                table: "Reviews",
                sql: "\"Rating\" >= 1 AND \"Rating\" <= 5");
        }

        /// <inheritdoc />
        // Removes the review rating constraint.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Reviews" RENAME TO "Reviews_Constrained";

                CREATE TABLE "Reviews" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Reviews" PRIMARY KEY AUTOINCREMENT,
                    "Rating" INTEGER NOT NULL,
                    "Comment" TEXT NOT NULL,
                    "UserId" TEXT NOT NULL,
                    "TrackId" INTEGER NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    CONSTRAINT "FK_Reviews_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_Reviews_Tracks_TrackId" FOREIGN KEY ("TrackId") REFERENCES "Tracks" ("Id") ON DELETE CASCADE
                );

                INSERT INTO "Reviews" ("Id", "Rating", "Comment", "UserId", "TrackId", "CreatedAt", "UpdatedAt")
                SELECT "Id", "Rating", "Comment", "UserId", "TrackId", "CreatedAt", "UpdatedAt"
                FROM "Reviews_Constrained";

                DROP TABLE "Reviews_Constrained";

                CREATE UNIQUE INDEX "IX_Reviews_TrackId_UserId" ON "Reviews" ("TrackId", "UserId");
                CREATE INDEX "IX_Reviews_UserId" ON "Reviews" ("UserId");
                """);
        }
    }
}
