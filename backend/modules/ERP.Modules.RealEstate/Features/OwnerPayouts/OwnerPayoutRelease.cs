using ERP.Application.Finance;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Voids a payout and reverses its journal (shared by void owner payout and the bill-released handler).</summary>
internal sealed class OwnerPayoutRelease(IJournalEntryRepository journals, IPostingService posting)
{
    public async Task ReleaseAsync(OwnerPayout payout, CancellationToken ct)
    {
        payout.Status = OwnerPayoutStatus.Voided;
        if (payout.JournalEntryId is not { } id) return;
        var journal = await journals.GetWithLinesForUpdateAsync(id, ct);
        await posting.ReverseTodayAsync(journal, $"Void owner payout {payout.Number}", ct);
    }
}
