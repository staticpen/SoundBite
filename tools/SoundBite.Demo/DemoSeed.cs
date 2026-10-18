using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Models;

namespace SoundBite.Demo;

public sealed record DemoLogin(string UserName, string Email, string Password);

public static class DemoSeed
{
    public static async Task<IReadOnlyList<DemoLogin>> PopulateAsync(ApplicationDbContext db, UserManager<ApplicationUser> manager)
    {
        if (await db.Users.AnyAsync() || await db.Artists.AnyAsync() || await db.Tracks.AnyAsync())
            throw new InvalidOperationException("Seeding requires a fresh migrated database.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        var now = DateTime.UtcNow;
        var names = new[] { "Alex River", "Sam Vale", "Jamie North" };
        var handles = new[] { "demo.alex", "demo.sam", "demo.jamie" };
        var users = new List<ApplicationUser>();
        var logins = new List<DemoLogin>();
        for (var i = 0; i < names.Length; i++)
        {
            var user = new ApplicationUser
            {
                Id = $"soundbite-demo-{i + 1}", UserName = handles[i], Email = $"{handles[i]}@example.invalid",
                DisplayName = names[i], EmailConfirmed = true,
                Bio = new[] { "Synthetic demo profile: indie melodies and late-night listening.", "Synthetic demo profile: jazz textures and weekend discoveries.", "Synthetic demo profile: electronic rhythms and road-trip playlists." }[i]
            };
            var password = "Sb9!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(18));
            var result = await manager.CreateAsync(user, password);
            if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
            users.Add(user);
            logins.Add(new DemoLogin(user.UserName, user.Email, password));
        }
        string[] artistNames = ["Harbour Lanterns", "Mira Solstice", "Paper Satellites", "Juniper Arcade", "The Amber Hours", "Nico Tide", "Velvet Observatory", "Cedar Signals", "Luna Transit", "Orchard Static"];
        string[] albumNames = ["Windows at Dusk", "Soft Geometry", "Letters from Orbit", "Garden Circuits", "After the Rain", "Coastal Frequency", "Midnight Telescope", "Forest Radio", "Last Train Home", "Summer Interference", "Northern Rooms", "Quiet Constellations", "Postcards in Blue", "Sunlit Machines", "Golden Weather"];
        string[] trackNames = ["Harbour Lights", "Glass Horizon", "Sunday Windows", "Small Satellites", "Garden Echo", "Amber Morning", "Coastal Drift", "Velvet Sky", "Cedar Path", "Last Train", "Orchard Bloom", "Paper Moon", "Slow Geometry", "Rain on Vinyl", "Distant Rooms", "Northern Air", "Quiet Signals", "Blue Postcard", "Sunlit Street", "Golden Hour", "Night Ferry", "Soft Landing", "Empty Platform", "City in Bloom", "Warm Static", "Silver Current", "Open Windows", "Long Way Home", "New Constellation", "Morning Circuit", "Faded Polaroid", "Tidal Memory", "Evening Arcade", "Faraway Garden", "Under the Canopy", "Satellite Heart", "Afterglow Avenue", "Daybreak Radio", "Summer Forecast", "Homeward Light"];
        var artists = artistNames.Select(name => new Artist { Name = name }).ToArray();
        var albums = albumNames.Select((title, i) => new Album { Title = title, Artists = [artists[i % 10]] }).ToArray();
        var tracks = trackNames.Select((title, i) => new Track { Title = title, Album = albums[i % 15], Artists = [artists[(i % 15) % 10]] }).ToArray();
        db.AddRange(artists); db.AddRange(albums); db.AddRange(tracks);
        var reviews = new List<Review>();
        string[] reviewText = ["Warm production and a memorable melody; a favourite for the evening walk.", "The layered rhythm rewards another listen. A thoughtful addition to this record.", "A strong chorus and an understated arrangement make this stand out."];
        for (var t = 0; t < tracks.Length; t++)
            for (var u = 0; u < (t < 10 ? 3 : 2); u++)
            {
                var when = now.AddHours(-(t * 2 + u + 2));
                reviews.Add(new Review { Track = tracks[t], UserId = users[u].Id, Rating = 2 + (t + u) % 4,
                    Comment = reviewText[u], CreatedAt = when, UpdatedAt = when });
            }
        db.AddRange(reviews);
        string[] playlistNames = ["Evening Harbour Walk", "Weekend Discoveries", "Road Trip Signals", "Quiet Study Hours", "Rainy Day Records", "Late Night Transit"];
        var playlists = playlistNames.Select((name, i) => new Playlist
        {
            Name = name, Description = "A curated synthetic collection for the SoundBite demonstration.",
            OwnerId = users[i % 3].Id, IsPublic = true, CreatedAt = now.AddDays(-3), UpdatedAt = now.AddHours(-2),
            Items = Enumerable.Range(0, 8).Select(j => new PlaylistItem { Track = tracks[(i * 5 + j) % 40], Position = j, AddedAt = now.AddDays(-2) }).ToList()
        }).ToArray();
        db.AddRange(playlists);
        foreach (var follower in users)
            foreach (var following in users.Where(user => user.Id != follower.Id))
            {
                db.Follows.Add(new Follow { FollowerId = follower.Id, FollowingId = following.Id });
                db.Notifications.Add(new Notification { RecipientUserId = following.Id, ActorUserId = follower.Id,
                    Type = NotificationType.Follow, CreatedAt = now.AddDays(-2), ReadAt = now.AddDays(-1) });
            }
        for (var i = 0; i < 18; i++)
        {
            var review = reviews[i];
            var ownerIndex = users.FindIndex(user => user.Id == review.UserId);
            var actor = users[(ownerIndex + 1) % 3];
            var parent = new Comment { Review = review, UserId = actor.Id, Content = "The rhythm section stood out to me too. Great recommendation!", CreatedAt = now.AddMinutes(-90 - i), UpdatedAt = now.AddMinutes(-90 - i) };
            var reply = new Comment { Review = review, UserId = review.UserId, ParentComment = parent, Content = "Thanks! Try the rest of the album for a similar sound.", CreatedAt = now.AddMinutes(-60 - i), UpdatedAt = now.AddMinutes(-60 - i) };
            var reviewLike = new Like { Review = review, UserId = actor.Id, CreatedAt = now.AddMinutes(-30 - i) };
            var commentLike = new Like { Comment = parent, UserId = review.UserId, CreatedAt = now.AddMinutes(-20 - i) };
            db.AddRange(parent, reply, reviewLike, commentLike);
            db.Likes.Add(new Like { Track = tracks[i], UserId = actor.Id, CreatedAt = now.AddMinutes(-40 - i) });
            db.Notifications.AddRange(
                new Notification { RecipientUserId = review.UserId, ActorUserId = actor.Id, Type = NotificationType.ReviewComment, Review = review, Comment = parent, CreatedAt = parent.CreatedAt },
                new Notification { RecipientUserId = actor.Id, ActorUserId = review.UserId, Type = NotificationType.CommentReply, Review = review, Comment = reply, CreatedAt = reply.CreatedAt },
                new Notification { RecipientUserId = review.UserId, ActorUserId = actor.Id, Type = NotificationType.ReviewLike, Review = review, Like = reviewLike, CreatedAt = reviewLike.CreatedAt },
                new Notification { RecipientUserId = actor.Id, ActorUserId = review.UserId, Type = NotificationType.CommentLike, Review = review, Like = commentLike, CreatedAt = commentLike.CreatedAt });
        }
        for (var i = 0; i < playlists.Length; i++)
        {
            var actor = users[(i + 1) % 3];
            var like = new Like { Playlist = playlists[i], UserId = actor.Id, CreatedAt = now.AddMinutes(-10 - i) };
            db.Likes.Add(like);
            db.Notifications.Add(new Notification { RecipientUserId = playlists[i].OwnerId, ActorUserId = actor.Id,
                Type = NotificationType.PlaylistLike, Playlist = playlists[i], Like = like, CreatedAt = like.CreatedAt });
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return logins;
    }
}
