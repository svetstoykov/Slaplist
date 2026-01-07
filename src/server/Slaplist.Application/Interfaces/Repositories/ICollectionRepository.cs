using Slaplist.Application.Domain;

namespace Slaplist.Application.Interfaces.Repositories;

public interface ICollectionRepository
{
    Task<Collection?> GetByIdAsync(int id, bool includeTracks = false, CancellationToken ct = default);
    
    /// <summary>
    /// Find collection by source and external ID.
    /// </summary>
    Task<Collection?> GetByExternalIdAsync(CollectionSource source, string externalId, bool includeTracks = false, CancellationToken ct = default);
    
    /// <summary>
    /// Get collections that need syncing (stale or never synced).
    /// </summary>
    Task<List<Collection>> GetNeedingSyncAsync(CollectionSource source, TimeSpan maxAge, int limit = 20, CancellationToken ct = default);
    
    /// <summary>
    /// Get all collections containing a specific track.
    /// </summary>
    Task<List<Collection>> GetContainingTrackAsync(int trackId, CancellationToken ct = default);
    
    Task<Collection> AddAsync(Collection collection, CancellationToken ct = default);
    Task UpdateAsync(Collection collection, CancellationToken ct = default);
    Task<int> GetTotalCountAsync(CancellationToken ct = default);
}