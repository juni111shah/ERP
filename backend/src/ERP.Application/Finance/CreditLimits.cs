using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;
using ERP.Domain.Parties;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

public interface ICreditLimitService
{
    /// <summary>
    /// Checks the party's open receivables in the legal entity, plus what the caller is about to add, against its credit limit.
    /// Over the limit it throws (409), or with the warn-only setting returns the message for the caller to show. Null when within the limit or the party has none.
    /// </summary>
    Task<string?> CheckAsync(Guid partyId, Guid legalEntityId, decimal additional, CancellationToken ct = default);
}

public sealed class CreditLimitService(IReadRepository<Party> parties, IReadRepository<Invoice> invoices, IReadRepository<Settlement> settlements, ISettingsService settings) : ICreditLimitService
{
    public const string WarnOnlySetting = "core.creditLimitWarnOnly";

    public async Task<string?> CheckAsync(Guid partyId, Guid legalEntityId, decimal additional, CancellationToken ct = default)
    {
        var party = await parties.Query(new Specification<Party>(p => p.Id == partyId)).Select(p => new { p.Name, p.CreditLimit }).FirstOrDefaultAsync(ct);
        if (party?.CreditLimit is not { } limit) return null;

        // Added up here because SQLite (tests) cannot sum decimals.
        var open = await invoices.Query(new Specification<Invoice>(i => i.PartyId == partyId && i.LegalEntityId == legalEntityId && (i.Status == DocumentStatus.Issued || i.Status == DocumentStatus.PartlyPaid)))
            .Select(i => new { i.IsCreditNote, i.Total, i.Paid }).ToListAsync(ct);
        var onAccount = await settlements.Query(new Specification<Settlement>(s => s.PartyId == partyId && s.LegalEntityId == legalEntityId && s.Direction == InstrumentDirection.Received && s.Status == DocumentStatus.Issued))
            .Select(s => s.Unallocated).ToListAsync(ct);
        var owed = open.Sum(i => (i.IsCreditNote ? -1 : 1) * (i.Total - i.Paid)) - onAccount.Sum();

        if (owed + additional <= limit) return null;
        var message = $"{party.Name} is over the credit limit of {limit:N2}: {owed:N2} is already owed and this adds {additional:N2}.";
        return await settings.GetAsync<bool?>(WarnOnlySetting, legalEntityId, ct) ?? false ? message : throw new ConflictException(message);
    }
}
