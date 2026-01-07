namespace Slaplist.API.Models;

public record RecommendationRequest(List<string> Tracks, int? CollectionsPerTrack, int? ResultsToReturn);

public record RecommendationResponse
{
    public required List<TrackResult> Recommendations { get; init; }
    public int TotalUniqueTracksFound { get; init; } 
    public int CollectionsProcessed { get; init; }
}

public record TrackResult
{
    public Guid Id { get; init; }
    public string Artist { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Label { get; init; }
    public string? Genre { get; init; }
    public int? Bpm { get; init; }
    public string? Key { get; init; }
    public int? ReleaseYear { get; init; }
    public string? YoutubeUrl { get; init; }
    public string? DiscogsUrl { get; init; }
    public List<CollectionSummary> AppearedInCollections { get; init; } = [];
}

public record CollectionSummary
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
}