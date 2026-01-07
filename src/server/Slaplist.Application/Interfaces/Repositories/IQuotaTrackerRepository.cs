using Slaplist.Application.Domain;

namespace Slaplist.Application.Interfaces.Repositories;

public interface IQuotaTrackerRepository
{
    /// <summary>
    /// Get or create today's quota tracker for a source.
    /// </summary>
    Task<QuotaTracker> GetOrCreateTodayAsync(CollectionSource source, CancellationToken ct = default);
    
    /// <summary>
    /// Increment usage.
    /// </summary>
    Task IncrementAsync(CollectionSource source, int units, int searchCalls = 0, int fetchCalls = 0, CancellationToken ct = default);
    
    /// <summary>
    /// Check if we can use X units without exceeding quota.
    /// </summary>
    Task<bool> CanUseAsync(CollectionSource source, int unitsNeeded, CancellationToken ct = default);
}