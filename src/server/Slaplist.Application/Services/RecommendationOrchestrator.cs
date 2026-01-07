using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Slaplist.Application.Domain;
using Slaplist.Application.Interfaces;
using Slaplist.Application.Models;
using Slaplist.Infrastructure.Data;

namespace Slaplist.Application.Services;

/// <summary>
/// Orchestrates the recommendation flow:
/// 1. Resolve input tracks (find/create in DB)
/// 2. Discover related collections (YouTube playlists, later Discogs/Bandcamp)
/// 3. Harvest tracks from collections
/// 4. Cross-reference and rank by frequency
/// 5. Return recommendations
/// </summary>
public class RecommendationOrchestrator : IRecommendationOrchestrator
{
    private readonly IYoutubeDiscoveryService _youtube;
    private readonly SlaplistDbContext _db;

    // Configuration
    private readonly TimeSpan _searchCacheMaxAge;
    private readonly TimeSpan _collectionSyncMaxAge;

    public RecommendationOrchestrator(
        IYoutubeDiscoveryService youtube,
        IOptions<RecommendationOptions> options,
        SlaplistDbContext db)
    {
        this._youtube = youtube;
        this._db = db;
        
        this._searchCacheMaxAge = TimeSpan.FromHours(options.Value.SearchCacheHours);
        this._collectionSyncMaxAge = TimeSpan.FromDays(options.Value.CollectionSyncDays);
    }

    /// <summary>
    /// Main entry point: get recommendations based on input tracks.
    /// </summary>
    public async Task<RecommendationResult> GetRecommendationsAsync(
        List<string> inputQueries,
        int collectionsPerTrack,
        int resultsToReturn,
        CancellationToken cancellationToken = default)
    {
        var stats = new OrchestratorStats();
        var trackScores = new Dictionary<int, TrackScore>();
        var processedCollectionIds = new HashSet<int>();
        var inputTrackIds = new HashSet<int>();
        
        foreach (var query in inputQueries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Find collections related to this query
            var collections = await this.DiscoverCollectionsAsync(query, collectionsPerTrack, stats, cancellationToken);

            // Process each collection
            var processed = 0;
            foreach (var collection in collections)
            {
                if (processed >= collectionsPerTrack) break;
                if (!processedCollectionIds.Add(collection.Id)) continue;

                // Ensure collection has tracks loaded
                await this.EnsureCollectionSyncedAsync(collection, stats, cancellationToken);

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

    /// <summary>
    /// Discover collections related to a query.
    /// Checks cache first, then calls YouTube API.
    /// </summary>
    private async Task<List<Collection>> DiscoverCollectionsAsync(
        string query,
        int maxCollections,
        OrchestratorStats stats,
        CancellationToken cancellationToken)
    {
        var normalizedQuery = SearchCache.Normalize(query);

        // Check cache first
        var cutoff = DateTime.UtcNow - this._searchCacheMaxAge;
        var cachedSearch = await this._db.SearchCaches
            .Where(s => s.NormalizedQuery == normalizedQuery &&
                        s.Source == CollectionSource.YouTube &&
                        s.SearchType == SearchType.PlaylistSearch &&
                        s.SearchedAt > cutoff)
            .OrderByDescending(s => s.SearchedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (cachedSearch is { ResultCollectionIds.Count: > 0 })
        {
            stats.CacheHits++;

            var wantedIds = cachedSearch.ResultCollectionIds.Take(maxCollections).ToList();
            var collections = await this._db.Collections
                .Where(c => wantedIds.Contains(c.Id))
                .Include(c => c.CollectionTracks)!.ThenInclude(ct2 => ct2.Track)
                .ToListAsync(cancellationToken);
            // Preserve cache order
            return collections.OrderBy(c => wantedIds.IndexOf(c.Id)).ToList();
        }

        // Check quota
        if (!await this.CanUseQuotaAsync(CollectionSource.YouTube, 100, cancellationToken))
        {
            stats.QuotaBlocked++;
            return [];
        }

        // Call YouTube API
        stats.ApiSearchCalls++;
        var searchResult = await this._youtube.SearchPlaylistsAsync(query + " playlist", maxCollections, cancellationToken);
        await this.IncrementQuotaAsync(CollectionSource.YouTube, searchResult.QuotaUsed, searchCalls: 1, cancellationToken: cancellationToken);


        // Persist collections and cache search
        var collections2 = new List<Collection>();

        foreach (var playlistInfo in searchResult.Playlists)
        {
            var existing = await this._db.Collections
                .Include(c => c.CollectionTracks)!.ThenInclude(ct2 => ct2.Track)
                .FirstOrDefaultAsync(c => c.Source == CollectionSource.YouTube && c.ExternalId == playlistInfo.PlaylistId, cancellationToken);

            if (existing != null)
            {
                collections2.Add(existing);
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
                this._db.Collections.Add(newCollection);
                collections2.Add(newCollection);
            }
        }

        // Persist any new collections so IDs are generated
        await this._db.SaveChangesAsync(cancellationToken);

        // Cache the search
        var collectionIds = collections2.Select(c => c.Id).ToList();
        this._db.SearchCaches.Add(new SearchCache
        {
            Query = query,
            NormalizedQuery = normalizedQuery,
            Source = CollectionSource.YouTube,
            SearchType = SearchType.PlaylistSearch,
            ResultCount = searchResult.Playlists.Count,
            QuotaUsed = searchResult.QuotaUsed,
            ResultCollectionIds = collectionIds
        });

        await this._db.SaveChangesAsync(cancellationToken);

        return collections2;
    }

    /// <summary>
    /// Ensure a collection has its tracks synced.
    /// </summary>
    private async Task EnsureCollectionSyncedAsync(
        Collection collection,
        OrchestratorStats stats,
        CancellationToken cancellationToken)
    {
        // Already synced and fresh?
        if (collection.SyncComplete && !collection.NeedsSync(this._collectionSyncMaxAge))
        {
            stats.CacheHits++;
            return;
        }

        // Check quota (playlist fetch is cheap: ~1 unit per 50 tracks)
        if (!await this.CanUseQuotaAsync(CollectionSource.YouTube, 5, cancellationToken))
        {
            stats.QuotaBlocked++;
            return;
        }

        // Fetch from YouTube
        stats.ApiFetchCalls++;
        var result = await this._youtube.GetPlaylistTracksAsync(collection.ExternalId, cancellationToken);
        await this.IncrementQuotaAsync(CollectionSource.YouTube, result.QuotaUsed, fetchCalls: 1, cancellationToken: cancellationToken);

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
            var track = await this.FindOrCreateTrackAsync(trackInfo, cancellationToken);

            collection.CollectionTracks.Add(new CollectionTrack
            {
                Track = track,
                Position = position++,
                DiscoveredAt = DateTime.UtcNow
            });
        }

        await this._db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Find existing track or create new one from YouTube data.
    /// </summary>
    private async Task<Track> FindOrCreateTrackAsync(YoutubeTrackInfo info, CancellationToken cancellationToken)
    {
        // Try to find by YouTube ID first
        var existing = await this._db.Tracks.FirstOrDefaultAsync(t => t.YoutubeVideoId == info.VideoId, cancellationToken);
        if (existing != null)
        {
            // Add raw title if we haven't seen it
            if (!existing.RawTitlesEncountered.Contains(info.RawTitle))
            {
                existing.RawTitlesEncountered.Add(info.RawTitle);
                existing.UpdatedAt = DateTime.UtcNow;
                // Will be saved by caller
            }
            return existing;
        }

        // Parse artist/title from raw YouTube title
        var (artist, title) = Track.ParseArtistTitle(info.RawTitle);

        // Try to find by normalized artist/title (might be same track from different source)
        var normalizedArtist = Track.NormalizeArtist(artist);
        var normalizedTitle = Track.NormalizeTitle(title);
        
        existing = await this._db.Tracks.FirstOrDefaultAsync(t => t.NormalizedArtist == normalizedArtist && t.NormalizedTitle == normalizedTitle, cancellationToken);
        if (existing != null)
        {
            // Same track, just add YouTube reference
            existing.YoutubeVideoId ??= info.VideoId;
            if (!existing.RawTitlesEncountered.Contains(info.RawTitle))
                existing.RawTitlesEncountered.Add(info.RawTitle);
            existing.UpdatedAt = DateTime.UtcNow;
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

        this._db.Tracks.Add(newTrack);
        return newTrack;
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

    // Private helpers for quota tracking using DbContext directly
    private async Task<QuotaTracker> GetOrCreateTodayQuotaAsync(CollectionSource source, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tracker = await this._db.QuotaTrackers.FirstOrDefaultAsync(q => q.Date == today && q.Source == source, cancellationToken);
        if (tracker == null)
        {
            tracker = new QuotaTracker
            {
                Date = today,
                Source = source,
                DailyLimit = QuotaTracker.GetDefaultLimit(source)
            };
            this._db.QuotaTrackers.Add(tracker);
            await this._db.SaveChangesAsync(cancellationToken);
        }

        return tracker;
    }

    private async Task<bool> CanUseQuotaAsync(CollectionSource source, int unitsNeeded, CancellationToken cancellationToken)
    {
        var tracker = await GetOrCreateTodayQuotaAsync(source, cancellationToken);
        return tracker.CanUse(unitsNeeded);
    }

    private async Task IncrementQuotaAsync(CollectionSource source, int units, int searchCalls = 0, int fetchCalls = 0, CancellationToken cancellationToken = default)
    {
        var tracker = await GetOrCreateTodayQuotaAsync(source, cancellationToken);
        tracker.UnitsUsed += units;
        tracker.SearchCalls += searchCalls;
        tracker.FetchCalls += fetchCalls;
        await this._db.SaveChangesAsync(cancellationToken);
    }
}