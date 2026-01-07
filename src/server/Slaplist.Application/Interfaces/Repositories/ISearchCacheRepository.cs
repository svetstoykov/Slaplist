using Slaplist.Application.Domain;

namespace Slaplist.Application.Interfaces.Repositories;

public interface ISearchCacheRepository
{
    /// <summary>
    /// Find a cached search result that's not expired.
    /// </summary>
    Task<SearchCache?> FindValidCacheAsync(
        string normalizedQuery, 
        CollectionSource source, 
        SearchType searchType,
        TimeSpan maxAge, 
        CancellationToken ct = default);
    
    Task<SearchCache> AddAsync(SearchCache cache, CancellationToken ct = default);
}