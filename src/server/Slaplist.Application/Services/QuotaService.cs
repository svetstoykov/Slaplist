using Microsoft.EntityFrameworkCore;
using Slaplist.Application.Data;
using Slaplist.Application.Domain;
using Slaplist.Application.Interfaces;

namespace Slaplist.Application.Services;

public class QuotaService(SlaplistDbContext db) : IQuotaService
{
    public async Task<bool> CanUseQuotaAsync(CollectionSource source, int unitsNeeded, CancellationToken ct)
    {
        var tracker = await this.GetOrCreateTodayQuotaAsync(source, ct);
        return tracker.CanUse(unitsNeeded);
    }

    public async Task IncrementQuotaAsync(CollectionSource source, int units, int searchCalls = 0, int fetchCalls = 0, CancellationToken ct = default)
    {
        var tracker = await this.GetOrCreateTodayQuotaAsync(source, ct);
        tracker.UnitsUsed += units;
        tracker.SearchCalls += searchCalls;
        tracker.FetchCalls += fetchCalls;
        await db.SaveChangesAsync(ct);
    }

    private async Task<QuotaTracker> GetOrCreateTodayQuotaAsync(CollectionSource source, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tracker = await db.QuotaTrackers.FirstOrDefaultAsync(q => q.Date == today && q.Source == source, ct);

        if (tracker != null) return tracker;

        tracker = new QuotaTracker { Date = today, Source = source, DailyLimit = QuotaTracker.GetDefaultLimit(source) };
        db.QuotaTrackers.Add(tracker);
        await db.SaveChangesAsync(ct);
        return tracker;
    }
}