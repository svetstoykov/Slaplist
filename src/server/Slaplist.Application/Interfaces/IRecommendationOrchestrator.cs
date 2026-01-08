using Slaplist.Application.Models;

namespace Slaplist.Application.Interfaces;

public interface IRecommendationOrchestrator
{
    Task<RecommendationResult> GetRecommendationsAsync(
        List<string> trackIds,
        int collectionsPerTrack,
        int resultsToReturn,
        CancellationToken cancellationToken = default);
}