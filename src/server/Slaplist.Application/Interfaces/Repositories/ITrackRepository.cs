using Slaplist.Application.Domain;

namespace Slaplist.Application.Interfaces.Repositories;

public interface ITrackRepository
{
    Task<Track?> GetByIdAsync(int id, CancellationToken ct = default);
    
    /// <summary>
    /// Find track by YouTube video ID.
    /// </summary>
    Task<Track?> GetByYoutubeIdAsync(string youtubeVideoId, CancellationToken ct = default);
    
    /// <summary>
    /// Find track by Discogs release ID.
    /// </summary>
    Task<Track?> GetByDiscogsIdAsync(string discogsReleaseId, CancellationToken ct = default);
    
    /// <summary>
    /// Find track by normalized artist + title (fuzzy match).
    /// </summary>
    Task<Track?> FindByArtistTitleAsync(string normalizedArtist, string normalizedTitle, CancellationToken ct = default);
    
    /// <summary>
    /// Search tracks by query (searches artist, title).
    /// </summary>
    Task<List<Track>> SearchAsync(string query, int limit = 50, CancellationToken ct = default);
    
    /// <summary>
    /// Get tracks that appear in the most collections (most "connected").
    /// </summary>
    Task<List<Track>> GetMostConnectedAsync(int limit = 50, CancellationToken ct = default);
    
    /// <summary>
    /// Get tracks needing enrichment (have artist/title but no metadata).
    /// </summary>
    Task<List<Track>> GetNeedingEnrichmentAsync(int limit = 100, CancellationToken ct = default);
    
    Task<Track> AddAsync(Track track, CancellationToken ct = default);
    Task UpdateAsync(Track track, CancellationToken ct = default);
    Task<int> GetTotalCountAsync(CancellationToken ct = default);
}
