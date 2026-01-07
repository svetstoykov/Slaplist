using Slaplist.Application.Models;

namespace Slaplist.Application.Interfaces;

/// <summary>
/// YouTube-specific discovery: find playlists, get playlist contents.
/// </summary>
public interface IYoutubeDiscoveryService
{
    /// <summary>
    /// Search for playlists containing/related to a query.
    /// Returns playlist metadata (not tracks).
    /// Cost: 100 quota units.
    /// </summary>
    Task<YoutubeSearchResult> SearchPlaylistsAsync(string query, int maxResults = 10, CancellationToken ct = default);
    
    /// <summary>
    /// Fetch all tracks from a playlist.
    /// Cost: ~1 quota unit per 50 tracks.
    /// </summary>
    Task<YoutubePlaylistResult> GetPlaylistTracksAsync(string playlistId, CancellationToken ct = default);
}