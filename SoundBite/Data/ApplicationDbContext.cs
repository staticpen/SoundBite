
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SoundBite.Models;

namespace SoundBite.Data
{
    public class ApplicationDbContext
        : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<Artist> Artists => Set<Artist>();
        public DbSet<Album> Albums => Set<Album>();
        public DbSet<Track> Tracks => Set<Track>();
        public DbSet<Review> Reviews => Set<Review>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Artist>()
                .HasMany(artist => artist.Albums)
                .WithMany(album => album.Artists)
                .UsingEntity("ArtistAlbums");

            builder.Entity<Artist>()
                .HasMany(artist => artist.Tracks)
                .WithMany(track => track.Artists)
                .UsingEntity("ArtistTracks");

            builder.Entity<Track>()
                .HasOne(track => track.Album)
                .WithMany(album => album.Tracks)
                .HasForeignKey(track => track.AlbumId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Review>()
                .HasOne(review => review.Track)
                .WithMany()
                .HasForeignKey(review => review.TrackId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Review>()
                .HasOne(review => review.User)
                .WithMany()
                .HasForeignKey(review => review.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Review>()
                .HasIndex(review => new { review.TrackId, review.UserId })
                .IsUnique();

            // SQLite unique indexes allow multiple NULLs for unknown external IDs.
            builder.Entity<Artist>().HasIndex(artist => artist.SpotifyId).IsUnique();
            builder.Entity<Album>().HasIndex(album => album.SpotifyId).IsUnique();
            builder.Entity<Track>().HasIndex(track => track.SpotifyId).IsUnique();
        }
    }
}