using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Options;
using Slaplist.Application.Common;
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

    public async Task<YoutubeSearchResult> SearchPlaylistsAsync(string query, int maxResults = 10, CancellationToken ct = default)
    {
        var request = this._youtube.Search.List("snippet");
        request.Q = query;
        request.Type = "playlist";
        request.MaxResults = Math.Min(maxResults, 50);

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
            .ToList();

        return new YoutubeSearchResult(playlists, QuotaUsed: YoutubeConstants.SearchListUnitCost);
    }

    public async Task<YoutubePlaylistResult> GetPlaylistTracksAsync(string playlistId, CancellationToken ct = default)
    {
        var tracks = new List<YoutubeTrackInfo>();
        string? nextPageToken = null;
        var quotaUsed = 0;

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
            QuotaUsed: quotaUsed
        );
    }
}