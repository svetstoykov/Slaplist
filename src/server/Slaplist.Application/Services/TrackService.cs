using Microsoft.EntityFrameworkCore;
using Slaplist.Application.Data;
using Slaplist.Application.Domain;
using Slaplist.Application.Interfaces;
using Slaplist.Application.Models;

namespace Slaplist.Application.Services;

public class TrackService(SlaplistDbContext db) : ITrackService
{
    public async Task<Track> FindOrCreateTrackAsync(YoutubeTrackInfo info, CancellationToken ct)
    {
        var existing = await db.Tracks.FirstOrDefaultAsync(t => t.YoutubeVideoId == info.VideoId, ct);

        if (existing != null)
        {
            if (!existing.RawTitlesEncountered.Contains(info.RawTitle))
            {
                existing.RawTitlesEncountered.Add(info.RawTitle);
                existing.UpdatedAt = DateTime.UtcNow;
            }
            return existing;
        }

        var (artist, title) = Track.ParseArtistTitle(info.RawTitle);
        var normalizedArtist = Track.NormalizeArtist(artist);
        var normalizedTitle = Track.NormalizeTitle(title);

        existing = await db.Tracks.FirstOrDefaultAsync(
            t => t.NormalizedArtist == normalizedArtist && t.NormalizedTitle == normalizedTitle, ct);

        if (existing != null)
        {
            existing.YoutubeVideoId ??= info.VideoId;
            if (!existing.RawTitlesEncountered.Contains(info.RawTitle))
                existing.RawTitlesEncountered.Add(info.RawTitle);
            existing.UpdatedAt = DateTime.UtcNow;
            return existing;
        }

        var track = new Track
        {
            Artist = artist,
            Title = title,
            NormalizedArtist = normalizedArtist,
            NormalizedTitle = normalizedTitle,
            YoutubeVideoId = info.VideoId,
            DurationSeconds = info.DurationSeconds,
            RawTitlesEncountered = [info.RawTitle]
        };

        db.Tracks.Add(track);
        return track;
    }

    public bool IsInputTrack(Track track, List<string> inputQueries)
    {
        var trackFull = $"{track.Artist} {track.Title}".ToLowerInvariant();
        return inputQueries.Any(q =>
        {
            var ql = q.ToLowerInvariant();
            return trackFull.Contains(ql) || ql.Contains(track.NormalizedTitle) || ql.Contains(track.NormalizedArtist);
        });
    }
}