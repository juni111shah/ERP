using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Repositories;

/// <summary>Data access for brokerage deals. Implemented in BrokerageDealRepository.cs.</summary>
public interface IBrokerageDealRepository : IRepository<BrokerageDeal>
{
    /// <summary>The deal when <paramref name="invoiceId"/> is its commission invoice, tracked for changes; null otherwise.</summary>
    Task<BrokerageDeal?> FindByCommissionInvoiceForUpdateAsync(Guid id, Guid invoiceId, CancellationToken ct);

    /// <summary>The deals dated from <paramref name="from"/> to <paramref name="to"/> that are not lost, of one legal entity or (null) all, with their commission.</summary>
    Task<List<DealCommissionRow>> ListCommissionRowsAsync(DateOnly from, DateOnly to, Guid? legalEntityId, CancellationToken ct);
}

/// <summary>A deal's agent, value, commission and shares.</summary>
public sealed record DealCommissionRow(Guid? AgentPartyId, string? ExternalAgentName, decimal DealValue, decimal GrossCommission, decimal AgentSharePercent, decimal TeamLeadSharePercent, DealStage Stage);
