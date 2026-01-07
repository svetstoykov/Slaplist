namespace Slaplist.Application.Domain;

/// <summary>
/// Caches search queries to avoid repeating expensive API calls.
/// "We searched for 'Surgeon - Force + Form' on YouTube 2 hours ago, here's what we found."
/// </summary>
public class SearchCache
{
    public int Id { get; set; }
    
    /// <summary>
    /// What was searched (original query).
    /// </summary>
    public string Query { get; set; } = string.Empty;
    
    /// <summary>
    /// Normalized for matching (lowercase, trimmed).
    /// </summary>
    public string NormalizedQuery { get; set; } = string.Empty;
    
    /// <summary>
    /// Where did we search.
    /// </summary>
    public CollectionSource Source { get; set; }
    
    /// <summary>
    /// What type of search (playlist search, track search, user search, etc.).
    /// </summary>
    public SearchType SearchType { get; set; }
    
    /// <summary>
    /// When the search was performed.
    /// </summary>
    public DateTime SearchedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// How many results came back.
    /// </summary>
    public int ResultCount { get; set; }
    
    /// <summary>
    /// API quota units consumed by this search.
    /// </summary>
    public int QuotaUsed { get; set; }
    
    /// <summary>
    /// Collection IDs returned by this search.
    /// </summary>
    public List<int> ResultCollectionIds { get; set; } = [];
    
    /// <summary>
    /// Track IDs returned by this search (for track searches).
    /// </summary>
    public List<int> ResultTrackIds { get; set; } = [];
    
    // ─────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────
    
    public bool IsExpired(TimeSpan maxAge) => DateTime.UtcNow - this.SearchedAt > maxAge;
    
    public bool IsExpired(int hours) => this.IsExpired(TimeSpan.FromHours(hours));
    
    public static string Normalize(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return string.Empty;
        return query.ToLowerInvariant().Trim();
    }
}

public enum SearchType
{
    /// <summary>
    /// Search for playlists containing a track/query (YouTube).
    /// </summary>
    PlaylistSearch = 1,
    
    /// <summary>
    /// Search for a track by name (Discogs, Bandcamp).
    /// </summary>
    TrackSearch = 2,
    
    /// <summary>
    /// Search for users who own/want a specific release (Discogs).
    /// </summary>
    UserSearch = 3,
    
    /// <summary>
    /// Search for sellers with a specific release (Discogs).
    /// </summary>
    SellerSearch = 4
}