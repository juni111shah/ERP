using ERP.Application.Abstractions;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Repositories;

internal sealed class RealEstateChequeRepository(IRepository<PaymentInstrument> inner) : RepositoryBase<PaymentInstrument>(inner), IRealEstateChequeRepository
{
    public async Task<Dictionary<Guid, decimal>> InCustodyTotalsOfPartiesAsync(List<Guid> partyIds, CancellationToken ct)
        => (await Query(new Specification<PaymentInstrument>(i => i.Direction == InstrumentDirection.Received && i.Status == InstrumentStatus.InCustody && i.PartyId != null && partyIds.Contains(i.PartyId.Value)))
            .Select(i => new { i.PartyId, i.Amount }).ToListAsync(ct)).GroupBy(i => i.PartyId!.Value).ToDictionary(g => g.Key, g => g.Sum(i => i.Amount));
}
