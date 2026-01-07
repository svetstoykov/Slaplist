using Slaplist.Application.Interfaces.Repositories;

namespace Slaplist.Application.Interfaces;

public interface IUnitOfWork
{
    ITrackRepository Tracks { get; }
    ICollectionRepository Collections { get; }
    ISearchCacheRepository SearchCache { get; }
    IQuotaTrackerRepository Quota { get; }
    
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}