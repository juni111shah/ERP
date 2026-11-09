using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Modules.RealEstate.Repositories;

/// <summary>What the rent roll reads from received cheques. Implemented in RealEstateChequeRepository.cs.</summary>
public interface IRealEstateChequeRepository : IRepository<PaymentInstrument>
{
    /// <summary>The amount of the received cheques in custody of each of the parties, added up per party.</summary>
    Task<Dictionary<Guid, decimal>> InCustodyTotalsOfPartiesAsync(List<Guid> partyIds, CancellationToken ct);
}
