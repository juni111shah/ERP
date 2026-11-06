using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;
using ERP.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

/// <summary>
/// Reports add amounts in each legal entity's base currency without converting them, so a report across entities with different base
/// currencies would be a meaningless sum. Such a scope is refused: the caller picks one legal entity. Only entities with posted entries
/// count, so a workspace with an unused second entity keeps its combined view.
/// </summary>
public interface IReportCurrencyGuard
{
    /// <summary>Throws a field error on <c>legalEntityId</c> when the scope mixes base currencies.</summary>
    Task EnsureSingleCurrencyAsync(Guid? legalEntityId, CancellationToken ct = default);
}

public sealed class ReportCurrencyGuard(IReadRepository<JournalEntry> journals, IReadRepository<LegalEntity> entities, ICurrentUser user) : IReportCurrencyGuard
{
    public async Task EnsureSingleCurrencyAsync(Guid? legalEntityId, CancellationToken ct = default)
    {
        if (legalEntityId is not null || user.LegalEntityId is not null) return; // one entity is one currency
        // The legal-entity filter of the repository limits this to the entities the caller may see.
        var inUse = await journals.Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft)).Select(j => j.LegalEntityId).Distinct().ToListAsync(ct);
        if (inUse.Count < 2) return;
        var currencies = await entities.Query(new Specification<LegalEntity>(e => inUse.Contains(e.Id))).Select(e => e.BaseCurrency).Distinct().ToListAsync(ct);
        if (currencies.Count > 1)
            throw new FieldValidationException("legalEntityId", $"These legal entities use different base currencies ({string.Join(", ", currencies.OrderBy(c => c))}), so their amounts cannot be added up. Choose one legal entity.");
    }
}
