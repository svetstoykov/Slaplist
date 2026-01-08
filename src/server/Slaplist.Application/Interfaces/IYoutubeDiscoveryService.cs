using Slaplist.Application.Models;

namespace Slaplist.Application.Interfaces;

/// <summary>
/// YouTube-specific discovery: find playlists, get playlist contents.
/// </summary>
public interface IYoutubeDiscoveryService
{
    /// <summary>
    /// Search for playlists containing a specific video.
    /// </summary>
    /// <param name="videoId">The ID of the video to search for.</param>
    /// <param name="maxResults">The maximum number of results to return. Default is 10.</param>
    /// <param name="excludedPlaylistNames">Optional list of playlist names to exclude from results.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the YouTube search results.</returns>
    Task<YoutubeSearchResult> SearchPlaylistsByVideoIdAsync(string videoId, int maxResults = 10, List<string>? excludedPlaylistNames = null, CancellationToken ct = default);

    /// <summary>
    /// Search for playlists containing/related to a query.
    /// Returns playlist metadata (not tracks).
    /// Cost: 100 quota units.
    /// </summary>
    /// <param name="query">The search query string.</param>
    /// <param name="maxResults">The maximum number of results to return. Default is 10.</param>
    /// <param name="excludedPlaylistNames">Optional list of playlist names to exclude from results.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the YouTube search results.</returns>
    Task<YoutubeSearchResult> SearchPlaylistsAsync(string query, int maxResults = 10, List<string>? excludedPlaylistNames = null, CancellationToken ct = default);

    /// <summary>
    /// Fetch all tracks from a playlist.
    /// Cost: ~1 quota unit per 50 tracks.
    /// </summary>
    /// <param name="playlistId">The ID of the playlist to retrieve tracks from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the playlist tracks.</returns>
    Task<YoutubePlaylistResult> GetPlaylistTracksAsync(string playlistId, CancellationToken ct = default);
}