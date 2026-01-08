using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Options;
using Slaplist.Application.Common;
using Slaplist.Application.Domain;
using Slaplist.Application.Interfaces;
using Slaplist.Application.Models;

namespace Slaplist.Application.Services;

public class YoutubeDiscoveryService : IYoutubeDiscoveryService
{
    private readonly YouTubeService _youtube;

    public YoutubeDiscoveryService(IOptions<YoutubeOptions> youtubeOptions)
    {
        this._youtube = new YouTubeService(new BaseClientService.Initializer
        {
            ApiKey = youtubeOptions.Value.ApiKey,
            ApplicationName = youtubeOptions.Value.ApplicationName
        });
    }
    
    public async Task<YoutubeSearchResult> SearchPlaylistsByVideoIdAsync(string videoId, int maxResults = 10, List<string>? excludedPlaylistNames = null, CancellationToken ct = default)
    {
        // First, get the video's metadata (costs 1 quota unit)
        var videoRequest = this._youtube.Videos.List("snippet");
        videoRequest.Id = videoId;
        var videoResponse = await videoRequest.ExecuteAsync(ct);
    
        var video = videoResponse.Items.FirstOrDefault();
        if (video == null)
            return new YoutubeSearchResult([], QuotaUsed: 1);
    
        // Build a smarter query from the video's actual metadata
        var channelName = video.Snippet.ChannelTitle;
        var title = video.Snippet.Title;
        
        var (artist, songName) = Track.ParseArtistTitle(title);

        var query = $"{title} playlist";
        if (songName.Equals(title, StringComparison.OrdinalIgnoreCase))
        {
            // In the case of non-artist title, the metadata is bad enough that we can't search for playlists by title alone, so we add the channel name
            // This is not an optimal solution, but it might provide better results than just searching for 'playlist'
            query = $"{channelName} {query}";
        }
        
        // Now search for playlists with this enriched query
        return await this.SearchPlaylistsAsync(query, maxResults, excludedPlaylistNames, ct: ct);
    }

    public async Task<YoutubeSearchResult> SearchPlaylistsAsync(string query, int maxResults = 10, List<string>? excludedPlaylistNames = null, CancellationToken ct = default)
    {
        var apiQuery = query;
        if (excludedPlaylistNames != null)
        {
            foreach (var name in excludedPlaylistNames)
            {
                // If the name has spaces, wrap it in escaped quotes: -"some name"
                var formattedExclusion = name.Contains(' ') ? $"-\"{name}\"" : $"-{name}";
                apiQuery += $" {formattedExclusion}";
            }
        }
        
        var request = this._youtube.Search.List("snippet");
        request.Q = apiQuery;
        request.Type = "playlist";
        request.MaxResults = maxResults + (excludedPlaylistNames?.Count ?? 0);

        var response = await request.ExecuteAsync(ct);

        var playlists = response.Items
            .Where(item => item.Id?.PlaylistId != null)
            .Select(item => new YoutubePlaylistInfo(
                PlaylistId: item.Id.PlaylistId,
                Title: item.Snippet.Title,
                ChannelTitle: item.Snippet.ChannelTitle,
                ThumbnailUrl: item.Snippet.Thumbnails?.Medium?.Url ?? item.Snippet.Thumbnails?.Default__?.Url,
                ItemCount: null // Not available in search results
            ))
            .Where(p => excludedPlaylistNames == null || !excludedPlaylistNames.Any(ex => p.Title.Equals(ex, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return new YoutubeSearchResult(playlists, QuotaUsed: YoutubeConstants.SearchListUnitCost);
    }

    public async Task<YoutubePlaylistResult> GetPlaylistTracksAsync(string playlistId, CancellationToken ct = default)
    {
        var tracks = new List<YoutubeTrackInfo>();
        string? nextPageToken = null;
        var quotaUsed = 0;
        var fetchCalls = 0;
        
        // Fetch all tracks
        do
        {
            var request = this._youtube.PlaylistItems.List("snippet,contentDetails");
            request.PlaylistId = playlistId;
            request.MaxResults = 50;
            request.PageToken = nextPageToken;

            try
            {
                var response = await request.ExecuteAsync(ct);
                
                fetchCalls++;
                quotaUsed += YoutubeConstants.ListPlaylistItemsUnitCost;

                foreach (var item in response.Items)
                {
                    // Skip unavailable videos
                    if (item.Snippet.Title is "Deleted video" or "Private video")
                        continue;

                    tracks.Add(new YoutubeTrackInfo(
                        VideoId: item.Snippet.ResourceId.VideoId,
                        RawTitle: item.Snippet.Title,
                        ChannelTitle: item.Snippet.VideoOwnerChannelTitle ?? "Unknown",
                        ThumbnailUrl: item.Snippet.Thumbnails?.Medium?.Url ?? item.Snippet.Thumbnails?.Default__?.Url,
                        DurationSeconds: null // Would need Videos.list call, not worth the quota
                    ));
                }

                nextPageToken = response.NextPageToken;

                // Small delay between pages
                if (nextPageToken != null)
                    await Task.Delay(50, ct);
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                break;
            }
        } while (nextPageToken != null);

        return new YoutubePlaylistResult(
            PlaylistId: playlistId,
            Tracks: tracks,
            QuotaUsed: quotaUsed,
            FetchCalls: fetchCalls
        );
    }
}