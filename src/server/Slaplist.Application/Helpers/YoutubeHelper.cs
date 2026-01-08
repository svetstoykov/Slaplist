namespace Slaplist.Application.Helpers;

public static class YoutubeHelper
{
    /// <summary>
    /// Extracts YouTube video ID from various URL formats.
    /// Supports: youtube.com/watch?v=, youtu.be/, /embed/, /v/, /shorts/, or raw ID.
    /// </summary>
    public static string? ExtractVideoId(string youtubeUrl)
    {
        if (string.IsNullOrWhiteSpace(youtubeUrl))
            return null;

        youtubeUrl = youtubeUrl.Trim();

        // Already a video ID? (11 chars, alphanumeric + dash + underscore)
        if (youtubeUrl.Length == 11 && IsValidVideoId(youtubeUrl))
            return youtubeUrl;

        // Try parsing as URL
        if (Uri.TryCreate(youtubeUrl, UriKind.Absolute, out var uri))
        {
            // youtube.com/watch?v=VIDEO_ID
            if (uri.Host.Contains("youtube.com"))
            {
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var videoId = query["v"];
                if (!string.IsNullOrEmpty(videoId) && IsValidVideoId(videoId))
                    return videoId;

                // /embed/VIDEO_ID, /v/VIDEO_ID, /shorts/VIDEO_ID
                var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments is ["embed" or "v" or "shorts", _, ..])
                {
                    var id = segments[1].Split('?')[0]; // Remove any query params
                    if (IsValidVideoId(id))
                        return id;
                }
            }

            // youtu.be/VIDEO_ID
            if (uri.Host == "youtu.be")
            {
                var id = uri.AbsolutePath.TrimStart('/').Split('?')[0];
                if (IsValidVideoId(id))
                    return id;
            }
        }

        return null;
    }

    private static bool IsValidVideoId(string id)
    {
        return id.Length == 11 && id.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
    }
}