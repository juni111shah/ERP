using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Finance;

internal sealed class PaymentInstrumentRepository(IRepository<PaymentInstrument> instruments)
    : RepositoryBase<PaymentInstrument>(instruments), IPaymentInstrumentRepository
{
    public Task<PaymentInstrument?> FindForUpdateAsync(Guid id, CancellationToken ct)
        => Tracked(new Specification<PaymentInstrument>(x => x.Id == id)).FirstOrDefaultAsync(ct);

    public async Task<PaymentInstrument> GetIssuedForUpdateAsync(Guid id, CancellationToken ct)
        => await Tracked(new Specification<PaymentInstrument>(x => x.Id == id && x.Direction == InstrumentDirection.Issued)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public async Task<InstrumentInfo?> GetInfoAsync(Guid? id, CancellationToken ct)
    {
        if (id is not { } iid) return null;
        var i = await Query(new Specification<PaymentInstrument>(x => x.Id == iid))
            .Select(x => new { x.Kind, x.InstrumentNo, x.InstrumentDate, x.Status, x.BankName }).FirstOrDefaultAsync(ct);
        return i is null ? null : new InstrumentInfo(i.Kind.ToString(), i.InstrumentNo, i.InstrumentDate, i.Status.ToString(), i.BankName);
    }

    public Task<List<ChequeSummaryItem>> ListReceivedForSummaryAsync(CancellationToken ct)
        => Query(new Specification<PaymentInstrument>(i => i.Direction == InstrumentDirection.Received))
            .Select(i => new ChequeSummaryItem(i.Status, i.Amount, i.InstrumentDate)).ToListAsync(ct);

    public Task<Guid?> GetReplacementIdAsync(Guid id, CancellationToken ct)
        => Query(new Specification<PaymentInstrument>(x => x.ReplacesInstrumentId == id)).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
}
