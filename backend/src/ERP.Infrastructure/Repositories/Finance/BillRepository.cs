using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Portal;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Finance;

internal sealed class BillRepository(IRepository<Bill> bills) : RepositoryBase<Bill>(bills), IBillRepository
{
    public async Task<Bill> GetWithLinesAsync(Guid id, CancellationToken ct)
        => await Query(new Specification<Bill>(b => b.Id == id).Include(b => b.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public Task<Dictionary<Guid, string>> GetNumbersAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Bill>(b => ids.Contains(b.Id))).Select(b => new { b.Id, b.Number }).ToDictionaryAsync(b => b.Id, b => b.Number, ct);

    public Task<List<PortalBillDto>> ListForPortalAsync(Guid partyId, CancellationToken ct)
        => Query(new Specification<Bill>(b => b.PartyId == partyId && b.Status != DocumentStatus.Draft && b.Status != DocumentStatus.PendingApproval && b.Status != DocumentStatus.Voided))
            .OrderByDescending(b => b.BillDate).Take(200)
            .Select(b => new PortalBillDto(b.Id, b.Number, b.VendorReference, b.BillDate, b.DueDate, b.CurrencyCode, b.Total, b.Paid, b.Total - b.Paid, b.Status)).ToListAsync(ct);

    public Task<List<BillOpenItem>> ListOpenForPartyAsync(Guid partyId, DateOnly asOf, CancellationToken ct)
        => Query(new Specification<Bill>(b => b.PartyId == partyId && (b.Status == DocumentStatus.Issued || b.Status == DocumentStatus.PartlyPaid) && b.BillDate <= asOf))
            .Select(b => new BillOpenItem(b.Number, b.BillDate, b.DueDate, b.Total, b.Paid)).ToListAsync(ct);

    public Task<List<BillOpenBalance>> ListOpenBalancesAsync(Guid? legalEntityId, CancellationToken ct)
        => Query(new Specification<Bill>(b => (b.Status == DocumentStatus.Issued || b.Status == DocumentStatus.PartlyPaid) && (legalEntityId == null || b.LegalEntityId == legalEntityId)))
            .Select(b => new BillOpenBalance(b.PartyName, b.Total, b.Paid, b.DueDate)).ToListAsync(ct);
}
