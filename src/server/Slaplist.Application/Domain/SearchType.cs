namespace Slaplist.Application.Domain;

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