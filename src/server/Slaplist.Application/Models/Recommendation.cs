using Slaplist.Application.Domain;

namespace Slaplist.Application.Models;

public class RecommendationOptions
{
    public const string SectionName = nameof(RecommendationOptions);
    public int SearchCacheHours { get; set; } 
    public int CollectionSyncDays { get; set; } 
}

public class RecommendationResult
{
    public List<TrackScore> Recommendations { get; set; } = [];
    public OrchestratorStats Stats { get; set; } = new();
    public int TotalUniqueTracksFound { get; set; }
    public int CollectionsProcessed { get; set; }
}

public class TrackScore
{
    public Track Track { get; set; } = null!;
    public int Frequency { get; set; }
    public HashSet<string> FoundInCollections { get; set; } = [];
}

public class OrchestratorStats
{
    public int ApiSearchCalls { get; set; }
    public int ApiFetchCalls { get; set; }
    public int CacheHits { get; set; }
    public int NotEnoughQuota { get; set; }
    
    public int QuotaUsed { get; set; }

    public int TotalApiCalls => this.ApiSearchCalls + this.ApiFetchCalls;
    public int EstimatedQuotaUsed => (this.ApiSearchCalls * 100) + (this.ApiFetchCalls * 3);
}