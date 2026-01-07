namespace Slaplist.Application.Models;

public record YoutubeSearchResult(
    List<YoutubePlaylistInfo> Playlists,
    int QuotaUsed
);

public record YoutubePlaylistInfo(
    string PlaylistId,
    string Title,
    string? ChannelTitle,
    string? ThumbnailUrl,
    int? ItemCount
);

public record YoutubePlaylistResult(
    string PlaylistId,
    List<YoutubeTrackInfo> Tracks,
    int QuotaUsed,
    int FetchCalls
);

public record YoutubeTrackInfo(
    string VideoId,
    string RawTitle,           // Original title from YouTube
    string ChannelTitle,       // Usually the artist for music
    string? ThumbnailUrl,
    int? DurationSeconds
);

public class YoutubeOptions
{
    public const string SectionName = nameof(YoutubeOptions);
    public string ApiKey { get; set; }
    public string ApplicationName { get; set; }
}
