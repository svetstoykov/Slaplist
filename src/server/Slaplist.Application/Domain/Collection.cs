namespace Slaplist.Application.Domain;

/// <summary>
/// A collection of tracks curated by someone.
/// Generalizes: YouTube playlists, Discogs collections/wantlists, Bandcamp purchases/wishlists.
/// </summary>
public class Collection
{
    public Guid Id { get; set; }
    
    public CollectionSource Source { get; set; }
    public CollectionType Type { get; set; }
    
    /// <summary>
    /// External ID from the source platform.
    /// YouTube: playlist ID (e.g., "PLxyz...")
    /// Discogs: username (collection is per-user)
    /// Bandcamp: username
    /// </summary>
    public string ExternalId { get; set; } = string.Empty;
    
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    /// <summary>
    /// Who owns this collection.
    /// YouTube: channel name
    /// Discogs/Bandcamp: username
    /// </summary>
    public string? OwnerName { get; set; }
    public string? OwnerExternalId { get; set; }
    
    public string? ThumbnailUrl { get; set; }
    
    /// <summary>
    /// Track count as reported by the platform (may differ from actual stored tracks).
    /// </summary>
    public int ReportedTrackCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When we last fetched tracks from this collection.
    /// </summary>
    public DateTime? LastSyncedAt { get; set; }
    
    /// <summary>
    /// Whether sync completed or was partial (e.g., hit rate limit).
    /// </summary>
    public bool SyncComplete { get; set; }
    
    public ICollection<CollectionTrack> CollectionTracks { get; set; } = [];
    
    public int StoredTrackCount => this.CollectionTracks.Count;
    
    public bool NeedsSync(TimeSpan maxAge)
    {
        if (this.LastSyncedAt == null) return true;
        return DateTime.UtcNow - this.LastSyncedAt.Value > maxAge;
    }
    
    public bool NeedsSync(int days) => this.NeedsSync(TimeSpan.FromDays(days));
    
    public string Url =>
        this.Source switch
    {
        CollectionSource.YouTube => $"https://www.youtube.com/playlist?list={this.ExternalId}",
        CollectionSource.Discogs when this.Type == CollectionType.Collection 
            => $"https://www.discogs.com/user/{this.ExternalId}/collection",
        CollectionSource.Discogs when this.Type == CollectionType.Wantlist 
            => $"https://www.discogs.com/user/{this.ExternalId}/wantlist",
        CollectionSource.Discogs when this.Type == CollectionType.ForSale 
            => $"https://www.discogs.com/seller/{this.ExternalId}/profile",
        CollectionSource.Bandcamp 
            => $"https://bandcamp.com/{this.ExternalId}",
        _ => this.ExternalId
    };
}
