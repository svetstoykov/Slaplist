using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Slaplist.Application.Common;
using Slaplist.Application.Data;
using Slaplist.Application.Domain;
using Slaplist.Application.Interfaces;
using Slaplist.Application.Models;

public class RecommendationOrchestrator : IRecommendationOrchestrator
{
    private readonly IYoutubeDiscoveryService _youtube;
    private readonly IQuotaService _quotaService;
    private readonly ITrackService _tracksService;
    private readonly SlaplistDbContext _db;
    private readonly TimeSpan _searchCacheMaxAge;
    private readonly TimeSpan _collectionSyncMaxAge;

    public RecommendationOrchestrator(
        IYoutubeDiscoveryService youtube,
        IQuotaService quotaService,
        ITrackService tracksService,
        IOptions<RecommendationOptions> options,
        SlaplistDbContext db)
    {
        this._youtube = youtube;
        this._quotaService = quotaService;
        this._tracksService = tracksService;
        this._db = db;
        this._searchCacheMaxAge = TimeSpan.FromHours(options.Value.SearchCacheHours);
        this._collectionSyncMaxAge = TimeSpan.FromDays(options.Value.CollectionSyncDays);
    }

    public async Task<RecommendationResult> GetRecommendationsAsync(
        List<string> inputQueries,
        int collectionsPerTrack,
        int resultsToReturn,
        CancellationToken cancellationToken = default)
    {
        var stats = new OrchestratorStats();
        var trackScores = new Dictionary<Guid, TrackScore>();
        var processedCollectionIds = new HashSet<Guid>();
        var inputTrackIds = new HashSet<Guid>();

        foreach (var query in inputQueries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var collections = await this.DiscoverCollectionsAsync(query, collectionsPerTrack, stats, cancellationToken);
            var processed = 0;

            foreach (var collection in collections)
            {
                if (processed >= collectionsPerTrack) break;
                if (!processedCollectionIds.Add(collection.Id)) continue;

                await this.EnsureCollectionSyncedAsync(collection, stats, cancellationToken);

                foreach (var collectionTrack in collection.CollectionTracks)
                {
                    if (inputTrackIds.Contains(collectionTrack.TrackId)) continue;

                    if (this._tracksService.IsInputTrack(collectionTrack.Track, inputQueries))
                    {
                        inputTrackIds.Add(collectionTrack.TrackId);
                        continue;
                    }

                    if (trackScores.TryGetValue(collectionTrack.TrackId, out var score))
                    {
                        score.Frequency++;
                        score.FoundInCollections.Add(collection.Title);
                    }
                    else
                    {
                        trackScores[collectionTrack.TrackId] = new TrackScore
                        {
                            Track = collectionTrack.Track,
                            Frequency = 1,
                            FoundInCollections = [collection.Title]
                        };
                    }
                }
                processed++;
            }
        }

        var recommendations = trackScores.Values
            .OrderByDescending(s => s.Frequency)
            .ThenBy(s => s.Track.Title)
            .Take(resultsToReturn)
            .ToList();

        return new RecommendationResult
        {
            Recommendations = recommendations,
            Stats = stats,
            TotalUniqueTracksFound = trackScores.Count,
            CollectionsProcessed = processedCollectionIds.Count
        };
    }

    private async Task<List<Collection>> DiscoverCollectionsAsync(string query, int maxCollections, OrchestratorStats stats, CancellationToken ct)
    {
        var normalizedQuery = SearchCache.Normalize(query);
        var cutoff = DateTime.UtcNow - this._searchCacheMaxAge;

        var cachedSearch = await this._db.SearchCaches
            .Where(s => s.NormalizedQuery == normalizedQuery && s.Source == CollectionSource.YouTube && s.SearchType == SearchType.PlaylistSearch && s.SearchedAt > cutoff)
            .OrderByDescending(s => s.SearchedAt)
            .FirstOrDefaultAsync(ct);

        if (cachedSearch is { ResultCollectionIds.Count: > 0 })
        {
            stats.CacheHits++;
            var wantedIds = cachedSearch.ResultCollectionIds.Take(maxCollections).ToList();
            var collections = await this._db.Collections
                .Where(c => wantedIds.Contains(c.Id))
                .Include(c => c.CollectionTracks).ThenInclude(collectionTracks => collectionTracks.Track)
                .ToListAsync(ct);

            return collections.OrderBy(c => wantedIds.IndexOf(c.Id)).ToList();
        }

        if (!await this._quotaService.CanUseQuotaAsync(CollectionSource.YouTube, YoutubeConstants.SearchListUnitCost, ct))
        {
            stats.QuotaBlocked++;
            return [];
        }

        stats.ApiSearchCalls++;
        var searchResult = await this._youtube.SearchPlaylistsAsync(query, maxCollections, ct);
        await this._quotaService.IncrementQuotaAsync(CollectionSource.YouTube, searchResult.QuotaUsed, searchCalls: 1, ct: ct);

        var collectionsToReturn = new List<Collection>();
        foreach (var playlist in searchResult.Playlists)
        {
            var existing = await this._db.Collections
                .Include(c => c.CollectionTracks).ThenInclude(collection => collection.Track)
                .FirstOrDefaultAsync(c => c.Source == CollectionSource.YouTube && c.ExternalId == playlist.PlaylistId, ct);

            if (existing != null)
            {
                collectionsToReturn.Add(existing);
                continue;
            }

            var collection = new Collection
            {
                Source = CollectionSource.YouTube,
                Type = CollectionType.Playlist,
                ExternalId = playlist.PlaylistId,
                Title = playlist.Title,
                OwnerName = playlist.ChannelTitle,
                ThumbnailUrl = playlist.ThumbnailUrl,
                ReportedTrackCount = playlist.ItemCount ?? 0
            };

            this._db.Collections.Add(collection);
            collectionsToReturn.Add(collection);
        }

        await this._db.SaveChangesAsync(ct);
        this._db.SearchCaches.Add(new SearchCache
        {
            Query = query,
            NormalizedQuery = normalizedQuery,
            Source = CollectionSource.YouTube,
            SearchType = SearchType.PlaylistSearch,
            ResultCount = searchResult.Playlists.Count,
            QuotaUsed = searchResult.QuotaUsed,
            ResultCollectionIds = collectionsToReturn.Select(c => c.Id).ToList()
        });
        await this._db.SaveChangesAsync(ct);

        return collectionsToReturn;
    }

    private async Task EnsureCollectionSyncedAsync(Collection collection, OrchestratorStats stats, CancellationToken ct)
    {
        if (collection.SyncComplete && !collection.NeedsSync(this._collectionSyncMaxAge))
        {
            stats.CacheHits++;
            return;
        }

        if (!await this._quotaService.CanUseQuotaAsync(CollectionSource.YouTube, YoutubeConstants.RecommendedQuotaNeededForListPlaylistItems, ct))
        {
            stats.QuotaBlocked++;
            return;
        }

        stats.ApiFetchCalls++;
        var result = await this._youtube.GetPlaylistTracksAsync(collection.ExternalId, ct);
        await this._quotaService.IncrementQuotaAsync(CollectionSource.YouTube, result.QuotaUsed, fetchCalls: 1, ct: ct);

        collection.LastSyncedAt = DateTime.UtcNow;
        collection.SyncComplete = true;
        collection.ReportedTrackCount = result.Tracks.Count;
        collection.CollectionTracks.Clear();

        var position = 0;
        foreach (var trackInfo in result.Tracks)
        {
            if (trackInfo.RawTitle is "Deleted video" or "Private video") continue;

            var track = await this._tracksService.FindOrCreateTrackAsync(trackInfo, ct);
            collection.CollectionTracks.Add(new CollectionTrack { Track = track, Position = position++, DiscoveredAt = DateTime.UtcNow });
        }

        await this._db.SaveChangesAsync(ct);
    }
}