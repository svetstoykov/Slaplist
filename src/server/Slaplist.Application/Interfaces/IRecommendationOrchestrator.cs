using Slaplist.Application.Models;

namespace Slaplist.Application.Interfaces;

public interface IRecommendationOrchestrator
{
    Task<RecommendationResult> GetRecommendationsAsync(
        List<string> inputQueries,
        int collectionsPerTrack,
        int resultsToReturn,
        CancellationToken cancellationToken = default);
}