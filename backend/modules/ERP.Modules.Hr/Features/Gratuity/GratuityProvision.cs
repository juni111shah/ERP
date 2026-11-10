using ERP.Application.Abstractions;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

/// <summary>Balance of the end-of-service provision account for one legal entity (credit minus debit, posted entries only).</summary>
public sealed class GratuityProvision(IReadRepository<JournalEntry> journals, IPostingService posting)
{
    public async Task<decimal> BalanceAsync(Guid legalEntityId, DateOnly asOf, CancellationToken ct)
        => await BalanceOfAsync(await posting.AccountForRoleAsync("payroll.gratuity.provision", ct), legalEntityId, asOf, ct);

    /// <summary>The same balance, of the provision account already looked up.</summary>
    public async Task<decimal> BalanceOfAsync(Guid account, Guid legalEntityId, DateOnly asOf, CancellationToken ct)
    {
        var movements = await journals.Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.LegalEntityId == legalEntityId && j.Date <= asOf))
            .SelectMany(j => j.Lines.Where(l => l.AccountId == account), (j, l) => new { l.Debit, l.Credit }).ToListAsync(ct);
        return movements.Sum(m => m.Credit - m.Debit);
    }
}
