
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
        public DbSet<Playlist> Playlists => Set<Playlist>();
        public DbSet<PlaylistItem> PlaylistItems => Set<PlaylistItem>();

        public DbSet<Follow> Follows => Set<Follow>();

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

            builder.Entity<Review>(entity =>
            {
                entity.HasOne(review => review.Track)
                    .WithMany()
                    .HasForeignKey(review => review.TrackId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(review => review.User)
                    .WithMany()
                    .HasForeignKey(review => review.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(review => new { review.TrackId, review.UserId })
                    .IsUnique();

                entity.ToTable(table => table.HasCheckConstraint(
                    "CK_Reviews_Rating",
                    "\"Rating\" >= 1 AND \"Rating\" <= 5"));
            });

            builder.Entity<Playlist>()
                .HasOne(playlist => playlist.Owner)
                .WithMany()
                .HasForeignKey(playlist => playlist.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PlaylistItem>()
                .HasOne(item => item.Playlist)
                .WithMany(playlist => playlist.Items)
                .HasForeignKey(item => item.PlaylistId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PlaylistItem>()
                .HasOne(item => item.Track)
                .WithMany()
                .HasForeignKey(item => item.TrackId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PlaylistItem>()
                .HasIndex(item => new { item.PlaylistId, item.TrackId })
                .IsUnique();

            builder.Entity<PlaylistItem>()
                .HasIndex(item => new { item.PlaylistId, item.Position });

            builder.Entity<Follow>(entity =>
            {
                entity.HasKey(f => new { f.FollowerId, f.FollowingId });
                entity.HasIndex(f => new { f.FollowingId, f.FollowerId });
                entity.HasOne(f => f.Follower).WithMany().HasForeignKey(f => f.FollowerId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(f => f.Following).WithMany().HasForeignKey(f => f.FollowingId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.ToTable(table => table.HasCheckConstraint(
                    "CK_Follows_NoSelfFollow", "\"FollowerId\" <> \"FollowingId\""));
            });

            // SQLite unique indexes allow multiple NULLs for unknown external IDs.
            builder.Entity<Artist>().HasIndex(artist => artist.SpotifyId).IsUnique();
            builder.Entity<Album>().HasIndex(album => album.SpotifyId).IsUnique();
            builder.Entity<Track>().HasIndex(track => track.SpotifyId).IsUnique();
        }
    }
}