using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Repositories;

internal sealed class BrokerageDealRepository(IRepository<BrokerageDeal> inner) : RepositoryBase<BrokerageDeal>(inner), IBrokerageDealRepository
{
    public Task<BrokerageDeal?> FindByCommissionInvoiceForUpdateAsync(Guid id, Guid invoiceId, CancellationToken ct)
        => Tracked(new Specification<BrokerageDeal>(d => d.Id == id && d.CommissionInvoiceId == invoiceId)).FirstOrDefaultAsync(ct);

    public Task<List<DealCommissionRow>> ListCommissionRowsAsync(DateOnly from, DateOnly to, Guid? legalEntityId, CancellationToken ct)
        => Query(new Specification<BrokerageDeal>(d => d.DealDate >= from && d.DealDate <= to && d.Stage != DealStage.Lost && (legalEntityId == null || d.LegalEntityId == legalEntityId)))
            .Select(d => new DealCommissionRow(d.AgentPartyId, d.ExternalAgentName, d.DealValue, d.GrossCommission, d.AgentSharePercent, d.TeamLeadSharePercent, d.Stage)).ToListAsync(ct);
}
