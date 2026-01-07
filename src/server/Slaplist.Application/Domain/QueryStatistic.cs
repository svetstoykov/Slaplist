namespace Slaplist.Application.Domain;

public class QueryStatistic
{
    public Guid Id { get; set; }
    public string[] InputQueries { get; set; }
    public int ApiSearchCalls { get; set; }
    public int ApiFetchCalls { get; set; }
    public int CacheHits { get; set; }
    public int NotEnoughQuota { get; set; }

    public int TotalApiCalls => this.ApiSearchCalls + this.ApiFetchCalls;
    public int QuotaUsed { get; set; }
    public DateTime StartedAt { get; set; } 
    public DateTime CompletedAt { get; set; }
}