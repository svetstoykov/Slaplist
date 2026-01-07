namespace Slaplist.Application.Domain;

/// <summary>
/// Links a Track to a Collection with position info.
/// </summary>
public class CollectionTrack
{
    public Guid CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;
    
    public Guid TrackId { get; set; }
    public Track Track { get; set; } = null!;
    
    /// <summary>
    /// Position in the collection (for ordered playlists).
    /// </summary>
    public int Position { get; set; }
        
    /// <summary>
    /// When this track was added to the collection (if known).
    /// </summary>
    public DateTime? AddedToCollectionAt { get; set; }
    
    /// <summary>
    /// When we discovered this association.
    /// </summary>
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;
}