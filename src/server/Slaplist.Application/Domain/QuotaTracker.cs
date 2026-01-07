namespace Slaplist.Application.Domain;

/// <summary>
/// Tracks daily API quota usage per source.
/// YouTube: 10,000 units/day (searches are expensive: 100 units each)
/// Discogs: 60 requests/minute (much more liberal)
/// Bandcamp: No official quota (be reasonable)
/// </summary>
public class QuotaTracker
{
    public Guid Id { get; set; }
    
    public DateOnly Date { get; set; }
    
    public CollectionSource Source { get; set; }
    
    /// <summary>
    /// Units/requests used today.
    /// </summary>
    public int UnitsUsed { get; set; }
    
    /// <summary>
    /// Number of search API calls (most expensive for YouTube).
    /// </summary>
    public int SearchCalls { get; set; }
    
    /// <summary>
    /// Number of collection/playlist fetch calls.
    /// </summary>
    public int FetchCalls { get; set; }
    
    /// <summary>
    /// Daily limit for this source.
    /// </summary>
    public int DailyLimit { get; set; }

    public int Remaining => Math.Max(0, this.DailyLimit - this.UnitsUsed);
    
    public bool IsExhausted => this.UnitsUsed >= this.DailyLimit;
    
    public double UsagePercent => this.DailyLimit > 0 
        ? Math.Round((double)this.UnitsUsed / this.DailyLimit * 100, 1) 
        : 100;
    
    public bool CanUse(int units) => this.Remaining >= units;
    
    /// <summary>
    /// Get default daily limit for a source.
    /// </summary>
    public static int GetDefaultLimit(CollectionSource source) => source switch
    {
        CollectionSource.YouTube => 10_000,     // YouTube is stingy
        CollectionSource.Discogs => 1_000,      // 60/min ≈ 86k/day, but let's be conservative
        CollectionSource.Bandcamp => 10_000,    // No official limit, self-imposed
        _ => 1_000
    };
}