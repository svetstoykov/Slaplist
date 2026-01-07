using Slaplist.Application.Domain;

namespace Slaplist.Application.Interfaces;

public interface IQuotaService
{
    Task<bool> CanUseQuotaAsync(CollectionSource source, int unitsNeeded, CancellationToken ct);
    Task IncrementQuotaAsync(CollectionSource source, int units, int searchCalls = 0, int fetchCalls = 0, CancellationToken ct = default);
}