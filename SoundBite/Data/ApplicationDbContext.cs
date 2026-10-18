
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
        public DbSet<DemoReview> DemoReviews => Set<DemoReview>();
        public DbSet<Playlist> Playlists => Set<Playlist>();
        public DbSet<PlaylistItem> PlaylistItems => Set<PlaylistItem>();

        public DbSet<Follow> Follows => Set<Follow>();
        public DbSet<Like> Likes => Set<Like>();
        public DbSet<Comment> Comments => Set<Comment>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<NotificationOptOut> NotificationOptOuts => Set<NotificationOptOut>();

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

            builder.Entity<DemoReview>(entity =>
            {
                entity.HasOne(review => review.Track)
                    .WithMany()
                    .HasForeignKey(review => review.TrackId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(review => review.SeedKey).IsUnique();
                entity.ToTable(table => table.HasCheckConstraint(
                    "CK_DemoReviews_Rating",
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

            builder.Entity<Like>(entity =>
            {
                entity.HasOne(like => like.Review)
                    .WithMany()
                    .HasForeignKey(like => like.ReviewId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(like => like.User)
                    .WithMany()
                    .HasForeignKey(like => like.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(like => like.Track)
                    .WithMany()
                    .HasForeignKey(like => like.TrackId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(like => like.Playlist)
                    .WithMany()
                    .HasForeignKey(like => like.PlaylistId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(like => like.Comment)
                    .WithMany()
                    .HasForeignKey(like => like.CommentId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(like => new { like.ReviewId, like.UserId })
                    .IsUnique().HasFilter("\"ReviewId\" IS NOT NULL");
                entity.HasIndex(like => new { like.TrackId, like.UserId })
                    .IsUnique().HasFilter("\"TrackId\" IS NOT NULL");
                entity.HasIndex(like => new { like.PlaylistId, like.UserId })
                    .IsUnique().HasFilter("\"PlaylistId\" IS NOT NULL");
                entity.HasIndex(like => new { like.CommentId, like.UserId })
                    .IsUnique().HasFilter("\"CommentId\" IS NOT NULL");
                entity.ToTable(table => table.HasCheckConstraint(
                    "CK_Likes_ExactlyOneTarget",
                    """
                    (CASE WHEN "ReviewId" IS NOT NULL THEN 1 ELSE 0 END) +
                    (CASE WHEN "TrackId" IS NOT NULL THEN 1 ELSE 0 END) +
                    (CASE WHEN "PlaylistId" IS NOT NULL THEN 1 ELSE 0 END) +
                    (CASE WHEN "CommentId" IS NOT NULL THEN 1 ELSE 0 END) = 1
                    """));
            });

            builder.Entity<Comment>(entity =>
            {
                entity.HasOne(comment => comment.Review)
                    .WithMany()
                    .HasForeignKey(comment => comment.ReviewId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(comment => comment.User)
                    .WithMany()
                    .HasForeignKey(comment => comment.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(comment => comment.ParentComment)
                    .WithMany(comment => comment.Replies)
                    .HasForeignKey(comment => comment.ParentCommentId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(comment => new { comment.ReviewId, comment.ParentCommentId, comment.CreatedAt });
                entity.ToTable(table => table.HasCheckConstraint(
                    "CK_Comments_Content", "length(trim(\"Content\")) > 0"));
                entity.ToTable(table => table.HasCheckConstraint(
                    "CK_Comments_NoSelfReply",
                    "\"ParentCommentId\" IS NULL OR \"ParentCommentId\" <> \"Id\""));
            });

            builder.Entity<Notification>(entity =>
            {
                entity.HasOne(notification => notification.Recipient)
                    .WithMany()
                    .HasForeignKey(notification => notification.RecipientUserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(notification => notification.Actor)
                    .WithMany()
                    .HasForeignKey(notification => notification.ActorUserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(notification => notification.Review)
                    .WithMany()
                    .HasForeignKey(notification => notification.ReviewId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(notification => notification.Comment)
                    .WithMany()
                    .HasForeignKey(notification => notification.CommentId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(notification => notification.Playlist)
                    .WithMany()
                    .HasForeignKey(notification => notification.PlaylistId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(notification => notification.Like)
                    .WithMany()
                    .HasForeignKey(notification => notification.LikeId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(notification => new { notification.RecipientUserId, notification.CreatedAt, notification.Id });
                entity.HasIndex(notification => new { notification.RecipientUserId, notification.ReadAt });
                entity.HasIndex(notification => notification.LikeId)
                    .IsUnique().HasFilter("\"LikeId\" IS NOT NULL");
                entity.HasIndex(notification => new { notification.CommentId, notification.RecipientUserId })
                    .IsUnique().HasFilter("\"CommentId\" IS NOT NULL");
            });

            builder.Entity<NotificationOptOut>(entity =>
            {
                entity.HasKey(optOut => new { optOut.UserId, optOut.Type });
                entity.HasOne(optOut => optOut.User)
                    .WithMany()
                    .HasForeignKey(optOut => optOut.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // SQLite unique indexes allow multiple NULLs for unknown external IDs.
            builder.Entity<Artist>().HasIndex(artist => artist.SpotifyId).IsUnique();
            builder.Entity<Album>().HasIndex(album => album.SpotifyId).IsUnique();
            builder.Entity<Track>().HasIndex(track => track.SpotifyId).IsUnique();
        }
    }
}