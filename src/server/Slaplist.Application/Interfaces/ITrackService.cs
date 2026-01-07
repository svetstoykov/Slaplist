using Slaplist.Application.Domain;
using Slaplist.Application.Models;

namespace Slaplist.Application.Interfaces;

public interface ITrackService
{
    Task<Track> FindOrCreateTrackAsync(YoutubeTrackInfo info, CancellationToken ct);
    bool IsInputTrack(Track track, List<string> inputQueries);
}