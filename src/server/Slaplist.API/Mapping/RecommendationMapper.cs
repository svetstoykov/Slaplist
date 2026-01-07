using Slaplist.API.Models;
using Slaplist.Application.Models;

namespace Slaplist.API.Mapping;

public static class RecommendationMapper
{
    public static RecommendationResponse ToRecommendationResponse(RecommendationResult recommendationResult)
    {
        var response = new RecommendationResponse()
        {
            CollectionsProcessed = recommendationResult.CollectionsProcessed,
            TotalUniqueTracksFound = recommendationResult.TotalUniqueTracksFound,
            Recommendations = recommendationResult.Recommendations.Select(r => new TrackResult()
            {
                Id = r.Track.Id,
                Artist = r.Track.Artist,
                Title = r.Track.Title,
                Label = r.Track.Label,
                Genre = r.Track.Genre,
                Bpm = r.Track.Bpm,
                Key = r.Track.Key,
                ReleaseYear = r.Track.ReleaseYear,
                YoutubeUrl = r.Track.YoutubeUrl,
                DiscogsUrl = r.Track.DiscogsUrl,
                AppearedInCollections = r.Track.CollectionTracks
                    .Select(summary => new CollectionSummary
                    {
                        Id = summary.CollectionId,
                        Name = summary.Collection.Title,
                        Source = summary.Collection.Source.ToString()
                    })
                    .ToList()
            }).ToList()
        };

        return response;
    }
}