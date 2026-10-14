namespace SoundBite.Models;

public class Review
{
    private static readonly HashSet<(int trackId, string review)> ExistingReviews = new();

    public int trackId { get; }
    public string reviewer { get; }
    public string comment { get; }
    public int rating { get; }

    public Review(int _trackId, string _reviewer, string _comment, int _rating ){
        
        // If for some reason, they were able to submit a review without a name.
        if (string.IsNullOrWhiteSpace(_reviewer)){
            throw new ArgumentException("Reviewer name cannot be empty.", nameof(_reviewer));
        }  

        var key = (_trackId, _reviewer);

        // One-review-per-track-per-reviewer rule.
        if(ExistingReviews.Contains(key)){
            throw new ArgumentException($"Reviewer '{_reviewer}' has already reviewed track ID {_trackId}.");
        }

        trackId = _trackId;
        reviewer = _reviewer;
        comment = _comment ?? string.Empty;
        rating = _rating;

        // Register this review
        ExistingReviews.Add(key);
    }

    // Clears all stored review records (for testing)
    public static void ResetRegistry(){
        ExistingReviews.Clear();
    }
}
