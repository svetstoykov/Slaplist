using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Slaplist.Application.Common;
using Slaplist.Application.Data;
using Slaplist.Application.Domain;
using Slaplist.Application.Interfaces;
using Slaplist.Application.Models;

namespace Slaplist.Application.Services;

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
        List<string> trackIds,
        int collectionsPerTrack,
        int resultsToReturn,
        CancellationToken cancellationToken = default)
    {
        var queryStatistic = new QueryStatistic() { InputQueries = trackIds.ToArray(), StartedAt = DateTime.UtcNow};
        var trackScores = new Dictionary<Guid, TrackScore>();
        var processedCollectionIds = new HashSet<Guid>();
        var inputTrackIds = new HashSet<Guid>();

        foreach (var trackId in trackIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var collections = await this.DiscoverCollectionsAsync(trackId, collectionsPerTrack, queryStatistic, cancellationToken);
            var processed = 0;

            foreach (var collection in collections)
            {
                if (processed >= collectionsPerTrack) break;
                if (!processedCollectionIds.Add(collection.Id)) continue;

                await this.EnsureCollectionSyncedAsync(collection, queryStatistic, cancellationToken);

                foreach (var collectionTrack in collection.CollectionTracks)
                {
                    if (inputTrackIds.Contains(collectionTrack.TrackId)) continue;

                    if (this._tracksService.IsInputTrack(collectionTrack.Track, trackIds))
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
        
        queryStatistic.CompletedAt = DateTime.UtcNow;
        this._db.QueryStatistics.Add(queryStatistic);
        await this._db.SaveChangesAsync(cancellationToken);
        
        return new RecommendationResult
        {
            Recommendations = recommendations,
            TotalUniqueTracksFound = trackScores.Count,
            CollectionsProcessed = processedCollectionIds.Count
        };
    }

    private async Task<List<Collection>> DiscoverCollectionsAsync(string trackId, int maxCollections, QueryStatistic stats, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow - this._searchCacheMaxAge;
        var collectionsToReturn = new List<Collection>();

        var cachedSearch = await this._db.SearchCaches
            .Where(s => s.Query == trackId && s.Source == CollectionSource.YouTube && s.SearchType == SearchType.PlaylistSearch &&
                        s.SearchedAt > cutoff)
            .OrderByDescending(s => s.SearchedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (cachedSearch is { ResultCollectionIds.Count: > 0 })
        {
            stats.CacheHits++;
            var wantedIds = cachedSearch.ResultCollectionIds.Take(maxCollections).ToList();
            var collections = await this._db.Collections
                .Where(c => wantedIds.Contains(c.Id))
                .Include(c => c.CollectionTracks).ThenInclude(collectionTracks => collectionTracks.Track)
                .ToListAsync(cancellationToken);

            return collections.OrderBy(c => wantedIds.IndexOf(c.Id)).ToList();
        }

        var cachedCollections = await this._db.Collections
            .Where(c => c.Source == CollectionSource.YouTube &&
                        c.CollectionTracks.Any(collectionTrack => collectionTrack.Track.YoutubeVideoId == trackId))
            .Include(c => c.CollectionTracks)
            .ThenInclude(collectionTrack => collectionTrack.Track)
            .ToListAsync(cancellationToken);

        if (cachedCollections.Count == maxCollections)
        {
            return cachedCollections;
        }

        if (cachedCollections.Count > 0)
        {
            collectionsToReturn.AddRange(cachedCollections);
        }

        if (!await this._quotaService.CanUseQuotaAsync(CollectionSource.YouTube, YoutubeConstants.SearchListUnitCost, cancellationToken))
        {
            stats.NotEnoughQuota++;
            return [];
        }

        stats.ApiSearchCalls++;
        var searchResult = await this._youtube.SearchPlaylistsByVideoIdAsync(trackId, maxCollections, cachedCollections.Select(cc => cc.Title).ToList(), cancellationToken);
        await this._quotaService.IncrementQuotaAsync(CollectionSource.YouTube, searchResult.QuotaUsed, searchCalls: 1, ct: cancellationToken);

        foreach (var playlist in searchResult.Playlists)
        {
            // We have to check again in case the playlist changed its name
            var existing = await this._db.Collections
                .Include(c => c.CollectionTracks)
                .ThenInclude(collection => collection.Track)
                .FirstOrDefaultAsync(c => c.Source == CollectionSource.YouTube && c.ExternalId == playlist.PlaylistId, cancellationToken);

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

        await this._db.SaveChangesAsync(cancellationToken);
        this._db.SearchCaches.Add(new SearchCache
        {
            Query = trackId,
            Source = CollectionSource.YouTube,
            SearchType = SearchType.PlaylistSearch,
            ResultCount = searchResult.Playlists.Count,
            QuotaUsed = searchResult.QuotaUsed,
            ResultCollectionIds = collectionsToReturn.Select(c => c.Id).ToList()
        });

        stats.QuotaUsed += searchResult.QuotaUsed;

        await this._db.SaveChangesAsync(cancellationToken);

        return collectionsToReturn;
    }

    private async Task EnsureCollectionSyncedAsync(Collection collection, QueryStatistic stats, CancellationToken cancellationToken)
    {
        if (collection.SyncComplete && !collection.NeedsSync(this._collectionSyncMaxAge))
        {
            stats.CacheHits++;
            return;
        }

        if (!await this._quotaService.CanUseQuotaAsync(CollectionSource.YouTube, YoutubeConstants.RecommendedQuotaNeededForListPlaylistItems,
                cancellationToken))
        {
            stats.NotEnoughQuota++;
            return;
        }

        stats.ApiFetchCalls++;
        var result = await this._youtube.GetPlaylistTracksAsync(collection.ExternalId, cancellationToken);
        await this._quotaService.IncrementQuotaAsync(CollectionSource.YouTube, result.QuotaUsed, fetchCalls: result.FetchCalls, ct: cancellationToken);
        
        collection.CollectionTracks.Clear();
        
        var uniqueVideoIds = result.Tracks.DistinctBy(t => t.VideoId).ToArray();
        for (var index = 0; index < uniqueVideoIds.Length; index++)
        {
            var trackInfo = uniqueVideoIds[index];
            if (trackInfo.RawTitle is "Deleted video" or "Private video") continue;

            var track = await this._tracksService.FindOrCreateTrackAsync(trackInfo, cancellationToken);
            collection.CollectionTracks.Add(new CollectionTrack { Track = track, Position = index + 1, DiscoveredAt = DateTime.UtcNow });
        }

        collection.LastSyncedAt = DateTime.UtcNow;
        collection.SyncComplete = true;
        collection.ReportedTrackCount = result.Tracks.Count;
        
        stats.QuotaUsed += result.QuotaUsed;

        await this._db.SaveChangesAsync(cancellationToken);
    }
}