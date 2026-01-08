using Slaplist.Application.Domain;

namespace Slaplist.Application.Domain;

/// <summary>
/// The atomic unit of Slaplist. A track represents a single real-world release.
/// Source-agnostic: a Surgeon track is a Surgeon track whether we found it on YouTube, Discogs, or Bandcamp.
/// </summary>
public class Track
{
    public Guid Id { get; set; }
    
    public string Artist { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    
    // Normalized versions for matching/deduping
    public string NormalizedArtist { get; set; } = string.Empty;
    public string NormalizedTitle { get; set; } = string.Empty;
    
    public string? Label { get; set; }
    public string? Genre { get; set; }
    public int? Bpm { get; set; }
    public string? Key { get; set; }
    public int? ReleaseYear { get; set; }
    public int? DurationSeconds { get; set; }
    
    public string? YoutubeVideoId { get; set; }
    public string? DiscogsReleaseId { get; set; }
    public string? DiscogsMasterId { get; set; }
    public string? BandcampUrl { get; set; }
    
    /// <summary>
    /// All the raw titles we've encountered for this track across sources.
    /// Useful for fuzzy matching and debugging.
    /// e.g., ["Surgeon - Force + Form", "Force + Form (Original Mix)", "unknown - track01"]
    /// </summary>
    public List<string> RawTitlesEncountered { get; set; } = [];
  
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When metadata was last enriched from Discogs/Bandcamp.
    /// Null means never enriched (only raw YouTube data).
    /// </summary>
    public DateTime? LastEnrichedAt { get; set; }
    
    public ICollection<CollectionTrack> CollectionTracks { get; set; } = [];
    
    
    public string? YoutubeUrl =>
        this.YoutubeVideoId != null 
        ? $"https://www.youtube.com/watch?v={this.YoutubeVideoId}" 
        : null;
    
    public string? DiscogsUrl =>
        this.DiscogsReleaseId != null 
        ? $"https://www.discogs.com/release/{this.DiscogsReleaseId}" 
        : null;
    
    public int CollectionCount => this.CollectionTracks.Count;
    
    public bool NeedsEnrichment => this.LastEnrichedAt == null && !string.IsNullOrEmpty(this.Artist);
    
    /// <summary>
    /// Normalize artist name for matching.
    /// </summary>
    public static string NormalizeArtist(string artist)
    {
        if (string.IsNullOrWhiteSpace(artist)) return string.Empty;
        
        return artist
            .ToLowerInvariant()
            .Replace(" - topic", "")      // YouTube auto-generated channels
            .Replace("vevo", "")
            .Trim();
    }
    
    /// <summary>
    /// Normalize track title for matching.
    /// </summary>
    public static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;
        
        return title
            .ToLowerInvariant()
            .Replace("(official video)", "")
            .Replace("(official audio)", "")
            .Replace("(official music video)", "")
            .Replace("[official video]", "")
            .Replace("[official audio]", "")
            .Replace("(original mix)", "")
            .Replace("(lyrics)", "")
            .Replace("[lyrics]", "")
            .Replace("(hd)", "")
            .Replace("[hd]", "")
            .Replace("(full)", "")
            .Trim();
    }
    
    /// <summary>
    /// Try to parse "Artist - Title" format common in YouTube.
    /// </summary>
    public static (string Artist, string Title) ParseArtistTitle(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return ("Unknown", "Unknown");
    
        // Common separators - order matters: check longer/more specific first
        var separators = new[] { " -- ", " - ", " – ", " — ", " | ", "--" };
    
        foreach (var sep in separators)
        {
            var idx = raw.IndexOf(sep, StringComparison.Ordinal);
            if (idx > 0)
            {
                var artist = raw[..idx].Trim();
                var title = raw[(idx + sep.Length)..].Trim();
            
                if (!string.IsNullOrEmpty(artist) && !string.IsNullOrEmpty(title))
                    return (artist, title);
            }
        }
    
        // Can't parse - treat whole thing as title
        return ("Unknown", raw.Trim());
    }
}