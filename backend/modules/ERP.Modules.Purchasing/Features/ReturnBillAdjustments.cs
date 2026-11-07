using ERP.Application.Abstractions;
using ERP.Application.Finance;
using ERP.Modules.Purchasing.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Purchasing.Features;

/// <summary>What purchase returns took off payables, so ageing agrees with the ledger: the part a bill absorbed, and any excess as credit with the vendor.</summary>
internal sealed class ReturnBillAdjustments(IReadRepository<PurchaseReturn> returns) : IBillAdjustmentSource
{
    public async Task<IReadOnlyList<BillAdjustment>> AsOfAsync(DateOnly asOf, Guid? legalEntityId, Guid? partyId, CancellationToken ct)
    {
        var cutoff = asOf.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var rows = await returns.Query(new Specification<PurchaseReturn>(r => r.ReturnDate <= asOf && r.Status != PurchaseReturnStatus.Draft && !(r.VoidedAt != null && r.VoidedAt < cutoff)
                && (legalEntityId == null || r.LegalEntityId == legalEntityId) && (partyId == null || r.VendorPartyId == partyId)))
            .SelectMany(r => r.BillShares, (r, s) => new { r.VendorPartyId, r.VendorName, s.BillId, s.Amount, s.AppliedToBill }).ToListAsync(ct);
        var result = new List<BillAdjustment>();
        foreach (var s in rows)
        {
            if (s.AppliedToBill > 0) result.Add(new BillAdjustment(s.VendorPartyId, s.VendorName, s.BillId, s.AppliedToBill));
            if (s.Amount > s.AppliedToBill) result.Add(new BillAdjustment(s.VendorPartyId, s.VendorName, null, s.Amount - s.AppliedToBill));
        }
        return result;
    }
}
