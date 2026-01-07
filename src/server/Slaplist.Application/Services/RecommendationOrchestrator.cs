using Slaplist.Application.Domain;
using Slaplist.Application.Interfaces;
using Slaplist.Application.Models;

namespace Slaplist.Application.Services;

/// <summary>
/// Orchestrates the recommendation flow:
/// 1. Resolve input tracks (find/create in DB)
/// 2. Discover related collections (YouTube playlists, later Discogs/Bandcamp)
/// 3. Harvest tracks from collections
/// 4. Cross-reference and rank by frequency
/// 5. Return recommendations
/// </summary>
public class RecommendationOrchestrator
{
    private readonly IUnitOfWork _uow;
    private readonly IYoutubeDiscoveryService _youtube;
    
    // Configuration
    private readonly TimeSpan _searchCacheMaxAge = TimeSpan.FromHours(24);
    private readonly TimeSpan _collectionSyncMaxAge = TimeSpan.FromDays(3);

    public RecommendationOrchestrator(IUnitOfWork uow, IYoutubeDiscoveryService youtube)
    {
        this._uow = uow;
        this._youtube = youtube;
    }

    /// <summary>
    /// Main entry point: get recommendations based on input tracks.
    /// </summary>
    public async Task<RecommendationResult> GetRecommendationsAsync(
        List<string> inputQueries,
        RecommendationOptions options,
        IProgress<ProgressUpdate>? progress = null,
        CancellationToken ct = default)
    {
        var stats = new OrchestratorStats();
        var trackScores = new Dictionary<int, TrackScore>();
        var processedCollectionIds = new HashSet<int>();
        var inputTrackIds = new HashSet<int>();
        
        foreach (var query in inputQueries)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(new ProgressUpdate($"Processing: {query}"));

            // Find collections related to this query
            var collections = await this.DiscoverCollectionsAsync(query, options.CollectionsPerTrack, stats, progress, ct);

            // Process each collection
            var processed = 0;
            foreach (var collection in collections)
            {
                if (processed >= options.CollectionsPerTrack) break;
                if (processedCollectionIds.Contains(collection.Id)) continue;

                processedCollectionIds.Add(collection.Id);

                // Ensure collection has tracks loaded
                await this.EnsureCollectionSyncedAsync(collection, stats, progress, ct);

                // Score each track in the collection
                foreach (var ct2 in collection.CollectionTracks)
                {
                    // Skip input tracks themselves
                    if (inputTrackIds.Contains(ct2.TrackId)) continue;
                    if (IsInputTrack(ct2.Track, inputQueries))
                    {
                        inputTrackIds.Add(ct2.TrackId);
                        continue;
                    }

                    if (trackScores.TryGetValue(ct2.TrackId, out var score))
                    {
                        score.Frequency++;
                        score.FoundInCollections.Add(collection.Title);
                    }
                    else
                    {
                        trackScores[ct2.TrackId] = new TrackScore
                        {
                            Track = ct2.Track,
                            Frequency = 1,
                            FoundInCollections = [collection.Title]
                        };
                    }
                }

                processed++;
                progress?.Report(new ProgressUpdate(
                    $"  Processed: {collection.Title} ({collection.StoredTrackCount} tracks)", 
                    IsDetail: true));
            }
        }
        
        var recommendations = trackScores.Values
            .OrderByDescending(s => s.Frequency)
            .ThenBy(s => s.Track.Title)
            .Take(options.ResultsToReturn)
            .ToList();

        return new RecommendationResult
        {
            Recommendations = recommendations,
            Stats = stats,
            TotalUniqueTracksFound = trackScores.Count,
            CollectionsProcessed = processedCollectionIds.Count
        };
    }

    /// <summary>
    /// Discover collections related to a query.
    /// Checks cache first, then calls YouTube API.
    /// </summary>
    private async Task<List<Collection>> DiscoverCollectionsAsync(
        string query,
        int maxCollections,
        OrchestratorStats stats,
        IProgress<ProgressUpdate>? progress,
        CancellationToken ct)
    {
        var normalizedQuery = SearchCache.Normalize(query);

        // Check cache first
        var cachedSearch = await this._uow.SearchCache.FindValidCacheAsync(
            normalizedQuery,
            CollectionSource.YouTube,
            SearchType.PlaylistSearch, this._searchCacheMaxAge,
            ct);

        if (cachedSearch != null && cachedSearch.ResultCollectionIds.Count > 0)
        {
            stats.CacheHits++;
            progress?.Report(new ProgressUpdate($"  [CACHE] Found {cachedSearch.ResultCollectionIds.Count} playlists", IsDetail: true));

            var collections = new List<Collection>();
            foreach (var colId in cachedSearch.ResultCollectionIds.Take(maxCollections))
            {
                var col = await this._uow.Collections.GetByIdAsync(colId, includeTracks: true, ct: ct);
                if (col != null) collections.Add(col);
            }
            return collections;
        }

        // Check quota
        if (!await this._uow.Quota.CanUseAsync(CollectionSource.YouTube, 100, ct))
        {
            stats.QuotaBlocked++;
            progress?.Report(new ProgressUpdate("  [QUOTA] Skipping - daily limit reached", IsDetail: true));
            return [];
        }

        // Call YouTube API
        stats.ApiSearchCalls++;
        var searchResult = await this._youtube.SearchPlaylistsAsync(query + " playlist", maxCollections, ct);
        await this._uow.Quota.IncrementAsync(CollectionSource.YouTube, searchResult.QuotaUsed, searchCalls: 1, ct: ct);

        progress?.Report(new ProgressUpdate($"  [API] Found {searchResult.Playlists.Count} playlists", IsDetail: true));

        // Persist collections and cache search
        var collections2 = new List<Collection>();
        var collectionIds = new List<int>();

        foreach (var playlistInfo in searchResult.Playlists)
        {
            var existing = await this._uow.Collections.GetByExternalIdAsync(
                CollectionSource.YouTube, playlistInfo.PlaylistId, includeTracks: true, ct: ct);

            if (existing != null)
            {
                collections2.Add(existing);
                collectionIds.Add(existing.Id);
            }
            else
            {
                var newCollection = new Collection
                {
                    Source = CollectionSource.YouTube,
                    Type = CollectionType.Playlist,
                    ExternalId = playlistInfo.PlaylistId,
                    Title = playlistInfo.Title,
                    OwnerName = playlistInfo.ChannelTitle,
                    ThumbnailUrl = playlistInfo.ThumbnailUrl,
                    ReportedTrackCount = playlistInfo.ItemCount ?? 0
                };
                
                await this._uow.Collections.AddAsync(newCollection, ct);
                await this._uow.SaveChangesAsync(ct);
                
                collections2.Add(newCollection);
                collectionIds.Add(newCollection.Id);
            }
        }

        // Cache the search
        await this._uow.SearchCache.AddAsync(new SearchCache
        {
            Query = query,
            NormalizedQuery = normalizedQuery,
            Source = CollectionSource.YouTube,
            SearchType = SearchType.PlaylistSearch,
            ResultCount = searchResult.Playlists.Count,
            QuotaUsed = searchResult.QuotaUsed,
            ResultCollectionIds = collectionIds
        }, ct);

        await this._uow.SaveChangesAsync(ct);

        return collections2;
    }

    /// <summary>
    /// Ensure a collection has its tracks synced.
    /// </summary>
    private async Task EnsureCollectionSyncedAsync(
        Collection collection,
        OrchestratorStats stats,
        IProgress<ProgressUpdate>? progress,
        CancellationToken ct)
    {
        // Already synced and fresh?
        if (collection.SyncComplete && !collection.NeedsSync(this._collectionSyncMaxAge))
        {
            stats.CacheHits++;
            return;
        }

        // Check quota (playlist fetch is cheap: ~1 unit per 50 tracks)
        if (!await this._uow.Quota.CanUseAsync(CollectionSource.YouTube, 5, ct))
        {
            stats.QuotaBlocked++;
            progress?.Report(new ProgressUpdate($"  [QUOTA] Skipping sync for {collection.Title}", IsDetail: true));
            return;
        }

        // Fetch from YouTube
        stats.ApiFetchCalls++;
        var result = await this._youtube.GetPlaylistTracksAsync(collection.ExternalId, ct);
        await this._uow.Quota.IncrementAsync(CollectionSource.YouTube, result.QuotaUsed, fetchCalls: 1, ct: ct);

        // Update collection metadata
        collection.Title = result.Title;
        collection.OwnerName = result.ChannelTitle;
        collection.LastSyncedAt = DateTime.UtcNow;
        collection.SyncComplete = true;
        collection.ReportedTrackCount = result.Tracks.Count;

        // Clear and rebuild tracks
        collection.CollectionTracks.Clear();

        var position = 0;
        foreach (var trackInfo in result.Tracks)
        {
            // Skip deleted/private
            if (trackInfo.RawTitle == "Deleted video" || trackInfo.RawTitle == "Private video")
                continue;

            // Find or create track
            var track = await this.FindOrCreateTrackAsync(trackInfo, ct);

            collection.CollectionTracks.Add(new CollectionTrack
            {
                Track = track,
                Position = position++,
                DiscoveredAt = DateTime.UtcNow
            });
        }

        await this._uow.Collections.UpdateAsync(collection, ct);
        await this._uow.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Find existing track or create new one from YouTube data.
    /// </summary>
    private async Task<Track> FindOrCreateTrackAsync(YoutubeTrackInfo info, CancellationToken ct)
    {
        // Try to find by YouTube ID first
        var existing = await this._uow.Tracks.GetByYoutubeIdAsync(info.VideoId, ct);
        if (existing != null)
        {
            // Add raw title if we haven't seen it
            if (!existing.RawTitlesEncountered.Contains(info.RawTitle))
            {
                existing.RawTitlesEncountered.Add(info.RawTitle);
                existing.UpdatedAt = DateTime.UtcNow;
                await this._uow.Tracks.UpdateAsync(existing, ct);
            }
            return existing;
        }

        // Parse artist/title from raw YouTube title
        var (artist, title) = Track.ParseArtistTitle(info.RawTitle);

        // Try to find by normalized artist/title (might be same track from different source)
        var normalizedArtist = Track.NormalizeArtist(artist);
        var normalizedTitle = Track.NormalizeTitle(title);
        
        existing = await this._uow.Tracks.FindByArtistTitleAsync(normalizedArtist, normalizedTitle, ct);
        if (existing != null)
        {
            // Same track, just add YouTube reference
            existing.YoutubeVideoId ??= info.VideoId;
            if (!existing.RawTitlesEncountered.Contains(info.RawTitle))
                existing.RawTitlesEncountered.Add(info.RawTitle);
            existing.UpdatedAt = DateTime.UtcNow;
            await this._uow.Tracks.UpdateAsync(existing, ct);
            return existing;
        }

        // Create new track
        var newTrack = new Track
        {
            Artist = artist,
            Title = title,
            NormalizedArtist = normalizedArtist,
            NormalizedTitle = normalizedTitle,
            YoutubeVideoId = info.VideoId,
            DurationSeconds = info.DurationSeconds,
            RawTitlesEncountered = [info.RawTitle]
        };

        return await this._uow.Tracks.AddAsync(newTrack, ct);
    }

    /// <summary>
    /// Check if a track matches one of the input queries.
    /// </summary>
    private static bool IsInputTrack(Track track, List<string> inputQueries)
    {
        var trackFull = $"{track.Artist} {track.Title}".ToLowerInvariant();
        
        return inputQueries.Any(q =>
        {
            var queryLower = q.ToLowerInvariant();
            return trackFull.Contains(queryLower) ||
                   queryLower.Contains(track.NormalizedTitle) ||
                   queryLower.Contains(track.NormalizedArtist);
        });
    }
}